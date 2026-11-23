using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Customers.Services;
using Sugarista.Application.Sms;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Auth.Commands.RequestCustomerOtp;

public record RequestCustomerOtpCommand(
    string PhoneNumber,
    string? RequestIp = null) : IRequest<RequestCustomerOtpResultDto>;

public record RequestCustomerOtpResultDto(
    bool Sent,
    string PhoneNumber,
    int ExpiresInSeconds,
    int RetryAfterSeconds,
    string Message,
    bool IsRateLimited = false);

public class RequestCustomerOtpCommandValidator : AbstractValidator<RequestCustomerOtpCommand>
{
    public RequestCustomerOtpCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(phone => SmsTextUtilities.NormalizeMobile(phone) is not null)
            .WithMessage("شماره موبایل معتبر وارد کنید (مثلاً 09123456789).");
    }
}

public class RequestCustomerOtpCommandHandler(
    IApplicationDbContext dbContext,
    ISmsGateway smsGateway) : IRequestHandler<RequestCustomerOtpCommand, RequestCustomerOtpResultDto>
{
    private const int SlidingWindowMinutes = 15;
    private const int MaxRequestsPerWindow = 5;

    public async Task<RequestCustomerOtpResultDto> Handle(
        RequestCustomerOtpCommand request,
        CancellationToken cancellationToken)
    {
        var smsMobile = SmsTextUtilities.NormalizeMobile(request.PhoneNumber);
        var phone = CustomerLoginRules.NormalizePhone(request.PhoneNumber);
        if (smsMobile is null || !CustomerLoginRules.IsValidPhone(phone))
        {
            return new RequestCustomerOtpResultDto(
                Sent: false,
                PhoneNumber: phone,
                ExpiresInSeconds: 0,
                RetryAfterSeconds: 0,
                Message: "شماره موبایل معتبر وارد کنید (مثلاً 09123456789).");
        }

        // Always capture & preserve the entered phone number in BOTH Customers and SmsContacts
        // so the cafe never loses customer phone numbers even if verification is abandoned.
        var customer = await CustomerDirectoryService.ResolveCustomerAsync(
            dbContext,
            customerName: null,
            phone,
            "online",
            cancellationToken);

        if (customer is null)
        {
            return new RequestCustomerOtpResultDto(
                Sent: false,
                PhoneNumber: phone,
                ExpiresInSeconds: 0,
                RetryAfterSeconds: 0,
                Message: "ثبت شماره موبایل ناموفق بود.");
        }

        var existingContact = await dbContext.SmsContacts
            .FirstOrDefaultAsync(c => c.Mobile == smsMobile, cancellationToken);
        if (existingContact is null)
        {
            var firstName = customer.PersianName != phone && customer.PersianName != "مهمان"
                ? customer.PersianName
                : null;
            dbContext.SmsContacts.Add(new SmsContact(
                firstName: firstName,
                lastName: null,
                mobile: smsMobile,
                email: customer.Email,
                notes: "ثبت خودکار از ورود پیامکی (OTP) سایت",
                isActive: true,
                marketingConsent: true));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!customer.IsActive)
        {
            return new RequestCustomerOtpResultDto(
                Sent: false,
                PhoneNumber: phone,
                ExpiresInSeconds: 0,
                RetryAfterSeconds: 0,
                Message: "حساب کاربری شما غیرفعال شده است. لطفاً با مدیریت کافه تماس بگیرید.");
        }

        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-SlidingWindowMinutes);

        var recentOtps = await dbContext.CustomerOtpCodes
            .Where(entry => entry.PhoneNumber == phone && entry.CreatedAtUtc >= windowStart)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var latestActive = recentOtps.FirstOrDefault(entry => !entry.IsConsumed);
        if (latestActive is not null)
        {
            var elapsedSeconds = (int)Math.Floor((now - latestActive.CreatedAtUtc).TotalSeconds);
            if (elapsedSeconds < CustomerOtpCode.DefaultCooldownSeconds)
            {
                var retryAfter = Math.Max(1, CustomerOtpCode.DefaultCooldownSeconds - elapsedSeconds);
                var remainingExpiry = Math.Max(0, (int)Math.Ceiling((latestActive.ExpiresAtUtc - now).TotalSeconds));
                return new RequestCustomerOtpResultDto(
                    Sent: false,
                    PhoneNumber: phone,
                    ExpiresInSeconds: remainingExpiry,
                    RetryAfterSeconds: retryAfter,
                    Message: $"کد تایید به‌تازگی ارسال شده است. لطفاً {retryAfter} ثانیه دیگر دوباره تلاش کنید.",
                    IsRateLimited: true);
            }
        }

        if (recentOtps.Count >= MaxRequestsPerWindow)
        {
            var oldestInWindow = recentOtps[^1];
            var retryAfter = Math.Max(60, (int)Math.Ceiling((oldestInWindow.CreatedAtUtc.AddMinutes(SlidingWindowMinutes) - now).TotalSeconds));
            return new RequestCustomerOtpResultDto(
                Sent: false,
                PhoneNumber: phone,
                ExpiresInSeconds: 0,
                RetryAfterSeconds: retryAfter,
                Message: "تعداد درخواست‌های کد تایید بیش از حد مجاز است. لطفاً چند دقیقه دیگر دوباره تلاش کنید.",
                IsRateLimited: true);
        }

        // Invalidate any previous unconsumed OTPs for this phone number.
        foreach (var stale in recentOtps.Where(entry => !entry.IsConsumed))
        {
            stale.Invalidate(now);
        }

        var code = CustomerOtpCode.GenerateNumericCode(CustomerOtpCode.DefaultCodeLength);
        var otpEntity = new CustomerOtpCode(
            phone,
            code,
            now,
            CustomerOtpCode.DefaultExpirySeconds,
            CustomerOtpCode.DefaultMaxAttempts,
            request.RequestIp);

        dbContext.CustomerOtpCodes.Add(otpEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var cafeName = await dbContext.SiteSettings
            .AsNoTracking()
            .Select(s => s.CafeName)
            .FirstOrDefaultAsync(cancellationToken);

        var smsOutcome = await smsGateway.SendOtpAsync(phone, code, cafeName, cancellationToken);
        if (!smsOutcome.Success)
        {
            // Release cooldown if the SMS provider failed to send so the user can retry immediately.
            dbContext.CustomerOtpCodes.Remove(otpEntity);
            await dbContext.SaveChangesAsync(cancellationToken);

            return new RequestCustomerOtpResultDto(
                Sent: false,
                PhoneNumber: phone,
                ExpiresInSeconds: 0,
                RetryAfterSeconds: 0,
                Message: string.IsNullOrWhiteSpace(smsOutcome.Error)
                    ? "ارسال پیامک کد تایید ناموفق بود. لطفاً چند لحظه دیگر دوباره تلاش کنید."
                    : smsOutcome.Error);
        }

        return new RequestCustomerOtpResultDto(
            Sent: true,
            PhoneNumber: phone,
            ExpiresInSeconds: CustomerOtpCode.DefaultExpirySeconds,
            RetryAfterSeconds: CustomerOtpCode.DefaultCooldownSeconds,
            Message: "کد تایید ۶ رقمی به شماره موبایل شما ارسال شد.");
    }
}

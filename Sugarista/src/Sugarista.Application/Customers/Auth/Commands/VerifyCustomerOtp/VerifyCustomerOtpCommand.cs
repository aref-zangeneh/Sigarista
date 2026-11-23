using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Common.Text;
using Sugarista.Application.Customers.Auth.Commands.LoginCustomer;
using Sugarista.Application.Customers.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Customers.Auth.Commands.VerifyCustomerOtp;

public record VerifyCustomerOtpCommand(
    string PhoneNumber,
    string Code) : IRequest<VerifyCustomerOtpResultDto>;

public record VerifyCustomerOtpResultDto(
    bool Success,
    string? Error,
    CustomerLoginResultDto? Login);

public class VerifyCustomerOtpCommandValidator : AbstractValidator<VerifyCustomerOtpCommand>
{
    public VerifyCustomerOtpCommandValidator()
    {
        RuleFor(command => command.PhoneNumber)
            .Must(phone => CustomerLoginRules.IsValidPhone(CustomerLoginRules.NormalizePhone(phone)))
            .WithMessage("شماره موبایل معتبر نیست.");

        RuleFor(command => command.Code)
            .NotEmpty()
            .WithMessage("کد تایید را وارد کنید.")
            .Must(code =>
            {
                var normalized = DigitNormalizer.ToEnglishDigits(code)?.Trim() ?? string.Empty;
                return normalized.Length is >= 4 and <= 8 && normalized.All(char.IsAsciiDigit);
            })
            .WithMessage("کد تایید باید عددی (۶ رقمی) باشد.");
    }
}

public class VerifyCustomerOtpCommandHandler(
    IApplicationDbContext dbContext) : IRequestHandler<VerifyCustomerOtpCommand, VerifyCustomerOtpResultDto>
{
    public async Task<VerifyCustomerOtpResultDto> Handle(
        VerifyCustomerOtpCommand request,
        CancellationToken cancellationToken)
    {
        var phone = CustomerLoginRules.NormalizePhone(request.PhoneNumber);
        var normalizedCode = DigitNormalizer.ToEnglishDigits(request.Code)?.Trim() ?? string.Empty;
        if (!CustomerLoginRules.IsValidPhone(phone) || string.IsNullOrWhiteSpace(normalizedCode))
        {
            return new VerifyCustomerOtpResultDto(false, "شماره موبایل یا کد تایید نامعتبر است.", null);
        }

        var now = DateTimeOffset.UtcNow;
        var otp = await dbContext.CustomerOtpCodes
            .Where(entry => entry.PhoneNumber == phone && entry.ConsumedAtUtc == null)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (otp is null)
        {
            return new VerifyCustomerOtpResultDto(false, "کد تایید فعالی یافت نشد. لطفاً دوباره کد دریافت کنید.", null);
        }

        if (otp.IsExpiredAt(now))
        {
            return new VerifyCustomerOtpResultDto(false, "مهلت اعتبار کد تایید به پایان رسیده است. لطفاً کد جدید دریافت کنید.", null);
        }

        if (otp.IsExhausted)
        {
            return new VerifyCustomerOtpResultDto(false, "تعداد تلاش‌های ناموفق بیش از حد مجاز است. لطفاً کد جدید دریافت کنید.", null);
        }

        if (!otp.VerifyAndConsume(normalizedCode, now))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            var remaining = Math.Max(0, otp.MaxAttempts - otp.AttemptCount);
            var error = remaining > 0
                ? $"کد تایید اشتباه است ({remaining} تلاش دیگر باقی‌مانده)."
                : "کد تایید اشتباه است و دیگر معتبر نیست. لطفاً کد جدید دریافت کنید.";
            return new VerifyCustomerOtpResultDto(false, error, null);
        }

        var customer = await CustomerDirectoryService.ResolveCustomerAsync(
            dbContext,
            customerName: null,
            phone,
            "online",
            cancellationToken);

        if (customer is null || !customer.IsActive)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new VerifyCustomerOtpResultDto(false, "حساب کاربری غیرفعال است.", null);
        }

        customer.MarkSeen("online");

        var settings = await dbContext.SiteSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken) ?? new Sugarista.Domain.Entities.SiteSettings();
        var durationDays = CustomerLoginRules.ClampDurationDays(settings.CustomerSessionDurationDays);

        var token = CustomerSession.NewToken();
        var session = new CustomerSession(customer.Id, token, now, durationDays);
        dbContext.CustomerSessions.Add(session);

        await dbContext.SaveChangesAsync(cancellationToken);

        var loginResult = new CustomerLoginResultDto(
            token,
            durationDays,
            session.ExpiresAtUtc,
            new CustomerIdentityDto(customer.Id, customer.PersianName, customer.PhoneNumber));

        return new VerifyCustomerOtpResultDto(true, null, loginResult);
    }
}

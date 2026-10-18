using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Orders.Services;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.ConfirmMockPayment;

public record ConfirmMockPaymentCommand(Guid OrderId, string Authority) : IRequest<ConfirmMockPaymentResultDto>;

public record ConfirmMockPaymentResultDto(
    Guid OrderId,
    string TrackingCode,
    string Status,
    decimal TotalAmount,
    DateTimeOffset? PaidAt);

public class ConfirmMockPaymentCommandValidator : AbstractValidator<ConfirmMockPaymentCommand>
{
    public ConfirmMockPaymentCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.Authority).NotEmpty();
    }
}

public class ConfirmMockPaymentCommandHandler : IRequestHandler<ConfirmMockPaymentCommand, ConfirmMockPaymentResultDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IPaymentGateway _paymentGateway;
    private readonly IAccountingService _accountingService;

    public ConfirmMockPaymentCommandHandler(
        IApplicationDbContext dbContext,
        IPaymentGateway paymentGateway,
        IAccountingService accountingService)
    {
        _dbContext = dbContext;
        _paymentGateway = paymentGateway;
        _accountingService = accountingService;
    }

    public async Task<ConfirmMockPaymentResultDto> Handle(ConfirmMockPaymentCommand request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.IsSplitBill)
        {
            throw new InvalidOperationException("Use split payment confirmation for split bill orders.");
        }

        if (!string.Equals(order.PaymentAuthority, request.Authority, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Payment authority does not match this order.");
        }

        if (order.Status == OrderStatus.Paid)
        {
            return new ConfirmMockPaymentResultDto(
                order.Id,
                order.TrackingCode,
                order.Status.ToString(),
                order.TotalAmount,
                order.PaidAt);
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOperationException($"Order cannot be paid while in status '{order.Status}'.");
        }

        var verification = await _paymentGateway.VerifyPaymentAsync(
            request.Authority,
            order.TotalAmount,
            cancellationToken);

        if (!verification.IsSuccessful)
        {
            order.MarkFailed();
            await _dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException(verification.Message ?? "Payment verification failed.");
        }

        order.MarkPaid();
        await OrderInventoryService.DeductInventoryForOrderLinesAsync(
            _dbContext,
            order,
            order.Lines.ToList(),
            cancellationToken);
        await DeliveryMethodInventoryService.DeductForOrderAsync(_dbContext, order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ConfirmMockPaymentResultDto(
            order.Id,
            order.TrackingCode,
            order.Status.ToString(),
            order.TotalAmount,
            order.PaidAt);
    }
}

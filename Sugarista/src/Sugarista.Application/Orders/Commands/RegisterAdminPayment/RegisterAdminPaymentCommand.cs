using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Accounting.Services;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.DeliveryMethods.Services;
using Sugarista.Application.Inventory.Services;
using Sugarista.Application.Orders.Services;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.RegisterAdminPayment;

/// <summary>
/// Finalizes an open invoice from the admin side: records the payment the cashier collected
/// (cash/card at the counter), marks the order paid and — in the same transaction —
/// deducts inventory (recipe lines + delivery packaging) and posts the accounting entry.
/// </summary>
public record RegisterAdminPaymentCommand(
    Guid OrderId,
    string PaymentMethod = "Cash",
    decimal? Amount = null) : IRequest<AdminOrderDetailDto>;

public class RegisterAdminPaymentCommandValidator : AbstractValidator<RegisterAdminPaymentCommand>
{
    public RegisterAdminPaymentCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();

        RuleFor(command => command.PaymentMethod)
            .Must(method => string.IsNullOrWhiteSpace(method) ||
                            Enum.TryParse<PosPaymentMethod>(method, true, out _))
            .WithMessage("Payment method must be Cash, Card, or OnlineGateway.");
    }
}

public class RegisterAdminPaymentCommandHandler : IRequestHandler<RegisterAdminPaymentCommand, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAccountingService _accountingService;

    public RegisterAdminPaymentCommandHandler(
        IApplicationDbContext dbContext,
        IAccountingService accountingService)
    {
        _dbContext = dbContext;
        _accountingService = accountingService;
    }

    public async Task<AdminOrderDetailDto> Handle(
        RegisterAdminPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        if (order.Status is OrderStatus.Paid or OrderStatus.Cancelled or OrderStatus.Failed)
        {
            throw new InvalidOperationException($"Invoice cannot be finalized in status '{order.Status}'.");
        }

        if (order.Status == OrderStatus.Held)
        {
            order.ResumeFromHold();
        }

        var remaining = order.TotalAmount - order.AmountPaid;
        if (remaining <= 0)
        {
            throw new InvalidOperationException("This invoice has no outstanding balance.");
        }

        var amount = request.Amount ?? remaining;
        if (amount <= 0 || amount > remaining)
        {
            throw new InvalidOperationException($"Payment amount must be between 0 and the outstanding {remaining:N0}.");
        }

        var parsedMethod = Enum.TryParse<PosPaymentMethod>(request.PaymentMethod, true, out var method)
            ? method
            : PosPaymentMethod.Cash;

        var payment = new OrderPayment(
            order.Id,
            payerKey: "admin",
            payerLabel: "ثبت توسط صندوق‌دار",
            amount,
            paymentAuthority: $"ADMIN-{parsedMethod}-{Guid.NewGuid():N}");
        payment.MarkPaid(referenceId: $"manual-{parsedMethod}");
        _dbContext.OrderPayments.Add(payment);
        order.AddPayment(payment);

        order.RecordPartialPayment(amount);

        // Financial + inventory events, together and immediately:
        await OrderInventoryService.DeductInventoryForOrderLinesAsync(
            _dbContext,
            order,
            order.Lines.ToList(),
            cancellationToken);
        await DeliveryMethodInventoryService.DeductForOrderAsync(_dbContext, order, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (order.Status == OrderStatus.Paid)
        {
            await _accountingService.PostOrderPaymentAsync(order, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return AdminOrderMapper.ToDetail(order);
    }
}

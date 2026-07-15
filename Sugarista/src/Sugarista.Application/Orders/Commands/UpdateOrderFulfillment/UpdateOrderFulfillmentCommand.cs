using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Orders.Commands.UpdateAdminOrder;
using Sugarista.Domain.Entities;

namespace Sugarista.Application.Orders.Commands.UpdateOrderFulfillment;

public record UpdateOrderFulfillmentCommand(Guid OrderId, FulfillmentStatus FulfillmentStatus)
    : IRequest<AdminOrderDetailDto>;

public class UpdateOrderFulfillmentCommandValidator : AbstractValidator<UpdateOrderFulfillmentCommand>
{
    public UpdateOrderFulfillmentCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.FulfillmentStatus)
            .Must(status => status is FulfillmentStatus.Registered
                or FulfillmentStatus.Preparing
                or FulfillmentStatus.Ready
                or FulfillmentStatus.Completed)
            .WithMessage("Invalid fulfillment status.");
    }
}

public class UpdateOrderFulfillmentCommandHandler : IRequestHandler<UpdateOrderFulfillmentCommand, AdminOrderDetailDto>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ISmsService _smsService;
    private readonly ILogger<UpdateOrderFulfillmentCommandHandler> _logger;

    public UpdateOrderFulfillmentCommandHandler(
        IApplicationDbContext dbContext,
        ISmsService smsService,
        ILogger<UpdateOrderFulfillmentCommandHandler> logger)
    {
        _dbContext = dbContext;
        _smsService = smsService;
        _logger = logger;
    }

    public async Task<AdminOrderDetailDto> Handle(
        UpdateOrderFulfillmentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("Order was not found.");

        order.UpdateFulfillment(request.FulfillmentStatus);

        if (request.FulfillmentStatus == FulfillmentStatus.Ready && order.ReadySmsSentAt is null)
        {
            try
            {
                await _smsService.SendOrderReadyAsync(
                    order.CustomerPhone,
                    order.TrackingCode,
                    order.CustomerName,
                    cancellationToken);
                order.MarkReadySmsSent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ready SMS for order {OrderId}", order.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return AdminOrderMapper.ToDetail(order);
    }
}

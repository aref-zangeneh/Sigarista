using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Maintenance.Commands.DeleteMaintenanceEquipment;

public record DeleteMaintenanceEquipmentCommand(Guid Id) : IRequest;

public class DeleteMaintenanceEquipmentCommandValidator : AbstractValidator<DeleteMaintenanceEquipmentCommand>
{
    public DeleteMaintenanceEquipmentCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
    }
}

public class DeleteMaintenanceEquipmentCommandHandler : IRequestHandler<DeleteMaintenanceEquipmentCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteMaintenanceEquipmentCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(DeleteMaintenanceEquipmentCommand request, CancellationToken cancellationToken)
    {
        var equipment = await _dbContext.MaintenanceEquipments
            .FirstOrDefaultAsync(item => item.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("تجهیز یافت نشد.");

        _dbContext.MaintenanceEquipments.Remove(equipment);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

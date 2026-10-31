using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Entities;
using FluentValidation;

namespace Sugarista.Application.Maintenance.Commands.UpsertMaintenanceEquipment;

public record UpsertMaintenanceEquipmentCommand(
    Guid? Id,
    string PersianName,
    string? Category,
    string? BrandModel,
    string? SerialNumber,
    string? Location,
    DateOnly? PurchaseDate,
    string? Notes,
    bool IsActive = true) : IRequest<Guid>;

public class UpsertMaintenanceEquipmentCommandValidator : AbstractValidator<UpsertMaintenanceEquipmentCommand>
{
    public UpsertMaintenanceEquipmentCommandValidator()
    {
        RuleFor(command => command.PersianName)
            .NotEmpty().WithMessage("نام تجهیز الزامی است.")
            .MaximumLength(150);

        RuleFor(command => command.Category).MaximumLength(60);
        RuleFor(command => command.BrandModel).MaximumLength(120);
        RuleFor(command => command.SerialNumber).MaximumLength(80);
        RuleFor(command => command.Location).MaximumLength(100);
        RuleFor(command => command.Notes).MaximumLength(500);
    }
}

public class UpsertMaintenanceEquipmentCommandHandler : IRequestHandler<UpsertMaintenanceEquipmentCommand, Guid>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertMaintenanceEquipmentCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> Handle(UpsertMaintenanceEquipmentCommand request, CancellationToken cancellationToken)
    {
        if (request.Id is { } equipmentId)
        {
            var existing = await _dbContext.MaintenanceEquipments
                .FirstOrDefaultAsync(equipment => equipment.Id == equipmentId, cancellationToken)
                ?? throw new KeyNotFoundException("تجهیز یافت نشد.");

            existing.Update(
                request.PersianName,
                request.Category,
                request.BrandModel,
                request.SerialNumber,
                request.Location,
                request.PurchaseDate,
                request.Notes);

            if (!request.IsActive && existing.IsActive)
            {
                existing.TakeOutOfService(null);
            }
            else if (request.IsActive && !existing.IsActive)
            {
                existing.BringBackToService();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return existing.Id;
        }

        var equipment = new MaintenanceEquipment(
            request.PersianName,
            request.Category,
            request.BrandModel,
            request.SerialNumber,
            request.Location,
            request.PurchaseDate,
            request.Notes);

        _dbContext.MaintenanceEquipments.Add(equipment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return equipment.Id;
    }
}

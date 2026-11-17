using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Warehouses.Commands.UpdateWarehouse;

public record UpdateWarehouseCommand(
    Guid Id,
    string Name,
    string? Code = null,
    string? Description = null,
    Guid? ParentId = null,
    int DisplayOrder = 0,
    bool IsActive = true) : IRequest<WarehouseDto>;

public class UpdateWarehouseCommandValidator : AbstractValidator<UpdateWarehouseCommand>
{
    public UpdateWarehouseCommandValidator()
    {
        RuleFor(c => c.Id)
            .NotEmpty();

        RuleFor(c => c.Name)
            .NotEmpty()
            .MaximumLength(Warehouse.MaxNameLength);

        RuleFor(c => c.Code)
            .MaximumLength(Warehouse.MaxCodeLength);

        RuleFor(c => c.Description)
            .MaximumLength(Warehouse.MaxDescriptionLength);

        RuleFor(c => c.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }
}

public class UpdateWarehouseCommandHandler
    : IRequestHandler<UpdateWarehouseCommand, WarehouseDto>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateWarehouseCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseDto> Handle(
        UpdateWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _dbContext.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Warehouse was not found.");

        if (request.ParentId is { } parentId)
        {
            if (parentId == warehouse.Id)
            {
                throw new Domain.Exceptions.DomainException("یک انبار نمی‌تواند والد خودش باشد.");
            }

            var parent = await _dbContext.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == parentId, cancellationToken)
                ?? throw new KeyNotFoundException("Parent warehouse was not found.");

            if (parent.IsSubWarehouse)
            {
                throw new DomainException("یک انبارک نمی‌تواند والد انبارک دیگر باشد؛ فقط انبار اصلی می‌تواند انبارک داشته باشد.");
            }
        }

        var name = request.Name.Trim();
        var nameTaken = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.Name == name && w.Id != warehouse.Id, cancellationToken);

        if (nameTaken)
        {
            throw new Domain.Exceptions.DomainException($"نام انبار «{name}» قبلاً استفاده شده است.");
        }

        warehouse.Update(name, request.Code, request.Description, request.DisplayOrder, request.IsActive);
        warehouse.MoveTo(request.ParentId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        string? parentName = null;
        if (warehouse.ParentId is { } updatedParentId)
        {
            parentName = await _dbContext.Warehouses
                .AsNoTracking()
                .Where(w => w.Id == updatedParentId)
                .Select(w => w.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var subWarehouseCount = await _dbContext.Warehouses
            .CountAsync(w => w.ParentId == warehouse.Id, cancellationToken);

        return warehouse.ToDto(parentName, subWarehouseCount);
    }
}

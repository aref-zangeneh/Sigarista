using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Application.Warehouses.Dto;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Warehouses.Commands.CreateWarehouse;

public record CreateWarehouseCommand(
    string Name,
    string? Code = null,
    string? Description = null,
    Guid? ParentId = null,
    int DisplayOrder = 0,
    bool IsActive = true) : IRequest<WarehouseDto>;

public class CreateWarehouseCommandValidator : AbstractValidator<CreateWarehouseCommand>
{
    public CreateWarehouseCommandValidator()
    {
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

public class CreateWarehouseCommandHandler
    : IRequestHandler<CreateWarehouseCommand, WarehouseDto>
{
    private readonly IApplicationDbContext _dbContext;

    public CreateWarehouseCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WarehouseDto> Handle(
        CreateWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        if (request.ParentId is { } parentId)
        {
            var parent = await _dbContext.Warehouses
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == parentId, cancellationToken)
                ?? throw new KeyNotFoundException("Parent warehouse was not found.");

            // Hierarchy is انبار → انبارک: only top-level warehouses may adopt.
            if (parent.IsSubWarehouse)
            {
                throw new DomainException("یک انبارک نمی‌تواند والد انبارک دیگر باشد؛ فقط انبار اصلی می‌تواند انبارک داشته باشد.");
            }
        }

        var name = request.Name.Trim();
        var nameTaken = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(w => w.Name == name, cancellationToken);

        if (nameTaken)
        {
            throw new DomainException($"نام انبار «{name}» قبلاً استفاده شده است.");
        }

        var warehouse = new Warehouse(
            name,
            request.Code,
            request.Description,
            request.ParentId,
            request.DisplayOrder,
            request.IsActive);

        _dbContext.Warehouses.Add(warehouse);
        await _dbContext.SaveChangesAsync(cancellationToken);

        string? parentName = null;
        if (warehouse.ParentId is { } createdParentId)
        {
            parentName = await _dbContext.Warehouses
                .AsNoTracking()
                .Where(w => w.Id == createdParentId)
                .Select(w => w.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return warehouse.ToDto(parentName, 0);
    }
}

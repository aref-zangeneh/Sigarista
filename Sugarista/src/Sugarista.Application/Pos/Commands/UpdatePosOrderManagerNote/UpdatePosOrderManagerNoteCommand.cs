using Microsoft.EntityFrameworkCore;
using Sugarista.Application.Common.Interfaces;
using Sugarista.Domain.Constants;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Pos.Commands.UpdatePosOrderManagerNote;

public record UpdatePosOrderManagerNoteCommand(
    Guid OrderId,
    string? ManagerNote) : IRequest<string?>;

public class UpdatePosOrderManagerNoteCommandValidator : AbstractValidator<UpdatePosOrderManagerNoteCommand>
{
    public UpdatePosOrderManagerNoteCommandValidator()
    {
        RuleFor(c => c.OrderId).NotEmpty();
        RuleFor(c => c.ManagerNote).MaximumLength(1000);
    }
}

public class UpdatePosOrderManagerNoteCommandHandler : IRequestHandler<UpdatePosOrderManagerNoteCommand, string?>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public UpdatePosOrderManagerNoteCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<string?> Handle(UpdatePosOrderManagerNoteCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.HasPermission(AdminPermissions.Pos.ManagerNote))
        {
            throw new DomainException("شما مجوز مشاهده یا ثبت یادداشت مدیریت را ندارید.");
        }

        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken)
            ?? throw new KeyNotFoundException("سفارش مورد نظر یافت نشد.");

        order.SetManagerNote(request.ManagerNote);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return order.ManagerNote;
    }
}

using Microsoft.EntityFrameworkCore;

namespace Sugarista.Application.Hr.Shifts.DeleteShiftPlan;

public record DeleteShiftPlanCommand(Guid Id) : IRequest;

public class DeleteShiftPlanCommandHandler : IRequestHandler<DeleteShiftPlanCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public DeleteShiftPlanCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        DeleteShiftPlanCommand request,
        CancellationToken cancellationToken)
    {
        var rowsDeleted = await _dbContext.EmployeeShiftPlans
            .Where(plan => plan.Id == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsDeleted == 0)
        {
            throw new KeyNotFoundException("Shift plan was not found.");
        }
    }
}

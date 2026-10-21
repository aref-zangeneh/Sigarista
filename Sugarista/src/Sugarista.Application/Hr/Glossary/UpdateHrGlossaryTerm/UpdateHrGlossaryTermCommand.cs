using Microsoft.EntityFrameworkCore;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.Hr.Glossary.UpdateHrGlossaryTerm;

public record UpdateHrGlossaryTermCommand(string Key, string Definition) : IRequest;

public class UpdateHrGlossaryTermCommandValidator : AbstractValidator<UpdateHrGlossaryTermCommand>
{
    public UpdateHrGlossaryTermCommandValidator()
    {
        RuleFor(command => command.Key)
            .NotEmpty()
            .MaximumLength(80);

        RuleFor(command => command.Definition)
            .NotEmpty()
            .MaximumLength(1200);
    }
}

public class UpdateHrGlossaryTermCommandHandler : IRequestHandler<UpdateHrGlossaryTermCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpdateHrGlossaryTermCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(
        UpdateHrGlossaryTermCommand request,
        CancellationToken cancellationToken)
    {
        var term = await _dbContext.HrGlossaryTerms
            .FirstOrDefaultAsync(existing => existing.Key == request.Key.Trim(), cancellationToken);

        if (term is not null)
        {
            term.Definition = request.Definition.Trim();
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var knownKey = Sugarista.Application.Hr.HrGlossaryDefaults.DefaultDefinitions
            .ContainsKey(request.Key.Trim());

        if (!knownKey)
        {
            throw new DomainException("این اصطلاح جزو اصطلاح‌های منابع انسانی نیست.");
        }

        _dbContext.HrGlossaryTerms.Add(
            new Sugarista.Domain.Entities.HrGlossaryTerm(request.Key, request.Definition));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Application.CostControl.Commands.SaveCostControlState;

public record SaveCostControlStateCommand(string State) : IRequest<SaveCostControlStateResultDto>;

public record SaveCostControlStateResultDto(long SavedAt, int Version);

public class SaveCostControlStateCommandValidator : AbstractValidator<SaveCostControlStateCommand>
{
    public SaveCostControlStateCommandValidator()
    {
        RuleFor(command => command.State)
            .NotEmpty()
            .WithMessage("حالت کاست کنترل برای ذخیره الزامی است.");
    }
}

public class SaveCostControlStateCommandHandler : IRequestHandler<SaveCostControlStateCommand, SaveCostControlStateResultDto>
{
    private const int MaxJsonLength = 4_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly IApplicationDbContext _dbContext;

    public SaveCostControlStateCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SaveCostControlStateResultDto> Handle(SaveCostControlStateCommand request, CancellationToken cancellationToken)
    {
        var json = request.State;

        if (json.Length > MaxJsonLength)
        {
            throw new DomainException("حجم داده‌ی کاست کنترل بیش از حد مجاز است.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new DomainException("قالب داده‌ی کاست کنترل معتبر نیست.");
            }
        }
        catch (JsonException)
        {
            throw new DomainException("قالب داده‌ی کاست کنترل معتبر نیست.");
        }

        var board = await _dbContext.CostControlBoards
            .OrderBy(existing => existing.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (board is null)
        {
            _dbContext.CostControlBoards.Add(new CostControlBoard(json, CostControlBoard.StateVersion));
        }
        else
        {
            board.Update(json, CostControlBoard.StateVersion);
            board.LastModifiedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveCostControlStateResultDto(now.ToUnixTimeMilliseconds(), CostControlBoard.StateVersion);
    }
}

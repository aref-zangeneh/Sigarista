using Sugarista.Domain.Entities;

namespace Sugarista.Application.Discounts.Engine;

/// <summary>
/// The result a discount type produces for one cart. The engine handles scope,
/// caps, stacking and usage limits, so an evaluator only has to answer
/// "how much does this discount take off?".
/// </summary>
public sealed record DiscountComputation(
    decimal Amount,
    IReadOnlyCollection<DiscountLineShare>? LineShares,
    string Message);

/// <summary>
/// One implementation per <see cref="DiscountType"/>. Registering a new class
/// is all it takes to add a new kind of discount: the engine discovers every
/// implementation through DI and dispatches on <see cref="Type"/>.
/// </summary>
public interface IDiscountEvaluator
{
    DiscountType Type { get; }

    /// <param name="discount">The rule being evaluated.</param>
    /// <param name="context">Cart, customer and timing information.</param>
    /// <param name="eligibleBase">Money still available to discount after scope and earlier stacks.</param>
    /// <param name="eligibleLines">The lines that make up <paramref name="eligibleBase"/>.</param>
    DiscountComputation? Evaluate(
        Discount discount,
        DiscountEvaluationContext context,
        decimal eligibleBase,
        IReadOnlyList<DiscountLineInput> eligibleLines);
}

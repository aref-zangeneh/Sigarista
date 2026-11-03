namespace Sugarista.Domain.Entities;

/// <summary>
/// Which eligible items are given away for free in a Buy-X-Get-Y offer.
/// </summary>
public enum FreeItemSelection
{
    /// <summary>Give away the cheapest eligible items (default; minimises the discount cost).</summary>
    Cheapest = 0,

    /// <summary>Give away the most expensive eligible items.</summary>
    MostExpensive = 1
}

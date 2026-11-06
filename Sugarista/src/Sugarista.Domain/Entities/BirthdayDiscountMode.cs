using Sugarista.Domain.Entities;

namespace Sugarista.Domain.Entities;

/// <summary>How the birthday benefit is delivered to the customer.</summary>
public enum BirthdayDiscountMode
{
    /// <summary>Applied automatically at checkout while the birthday window is active.</summary>
    Automatic = 0,

    /// <summary>Granted as a personal single-use discount code.</summary>
    Code = 1
}

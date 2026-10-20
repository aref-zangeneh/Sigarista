namespace Sugarista.Application.Exports;

/// <summary>Sections available in the export center.</summary>
public static class ExportSectionKinds
{
    public static readonly IReadOnlySet<string> KnownKinds = new HashSet<string>
    {
        "orders", "customers", "menu-items", "recipes", "inventory", "movements",
        "purchases", "suppliers", "delivery-methods", "accounting",
    };
}

using Sugarista.Domain.Common;

namespace Sugarista.Domain.Entities;

public class CostControlBoard : BaseAuditableEntity
{
    public const int StateVersion = 3;

    public CostControlBoard()
    {
    }

    public CostControlBoard(string json, int version)
    {
        Json = json;
        Version = version;
    }

    public string Json { get; set; } = string.Empty;

    public int Version { get; set; }

    public void Update(string json, int version)
    {
        Json = json;
        Version = version;
    }
}

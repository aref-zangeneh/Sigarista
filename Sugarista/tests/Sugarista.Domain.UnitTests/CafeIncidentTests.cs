using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class CafeIncidentTests
{
    [Fact]
    public void Constructor_ValidInputs_CreatesIncidentAndCalculatesDuration()
    {
        var occurred = new DateTimeOffset(2026, 11, 28, 10, 0, 0, TimeSpan.Zero);
        var resolved = occurred.AddMinutes(45);

        var incident = new CafeIncident(
            incidentCode: "INC-20261128-01",
            title: "قطع برق سالن و بار",
            category: IncidentCategory.PowerOutage,
            severity: IncidentSeverity.High,
            occurredAt: occurred,
            resolvedAt: resolved,
            status: IncidentStatus.Resolved,
            location: "سالن اصلی",
            estimatedLossAmount: 500_000m);

        Assert.Equal("INC-20261128-01", incident.IncidentCode);
        Assert.Equal("قطع برق سالن و بار", incident.Title);
        Assert.Equal(45, incident.DurationMinutes);
        Assert.Equal(500_000m, incident.EstimatedLossAmount);
    }

    [Fact]
    public void MarkResolved_BeforeOccurredAt_ThrowsDomainException()
    {
        var occurred = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        var incident = new CafeIncident("INC-20261128-02", "قطع اینترنت", IncidentCategory.InternetOrPos, IncidentSeverity.Medium, occurred);

        Assert.Throws<DomainException>(() =>
            incident.MarkResolved(occurred.AddMinutes(-10), "تست", "ادمین"));
    }

    [Fact]
    public void MarkResolved_ValidTimestamp_SetsStatusAndDuration()
    {
        var occurred = new DateTimeOffset(2026, 11, 28, 14, 0, 0, TimeSpan.Zero);
        var incident = new CafeIncident("INC-20261128-03", "خرابی آسیاب", IncidentCategory.EquipmentFailure, IncidentSeverity.Critical, occurred);

        incident.MarkResolved(occurred.AddMinutes(90), "تعویض فیوز و تست مجدد", "عارف");

        Assert.Equal(IncidentStatus.Resolved, incident.Status);
        Assert.Equal(90, incident.DurationMinutes);
        Assert.Equal("تعویض فیوز و تست مجدد", incident.ResolutionNotes);
        Assert.Equal("عارف", incident.ResolvedByName);
    }
}

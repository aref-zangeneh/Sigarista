using Sugarista.Application.ActivityLogs;

namespace Sugarista.Application.UnitTests;

public class ActivityLogNarratorTests
{
    [Fact]
    public void Describe_ShouldMentionChangedFieldsForUpdates()
    {
        var sentence = ActivityLogNarrator.Describe(
            "نیما",
            "سفارشات",
            ActivityActions.Update,
            "سفارش #SG-1234",
            ["وضعیت", "مبلغ"],
            succeeded: true,
            statusCode: 200);

        Assert.Contains("«نیما»", sentence);
        Assert.Contains("«سفارشات»", sentence);
        Assert.Contains("سفارش #SG-1234", sentence);
        Assert.Contains("وضعیت، مبلغ", sentence);
    }

    [Fact]
    public void Describe_ShouldReportFailedLoginWithoutLeakingPassword()
    {
        var sentence = ActivityLogNarrator.Describe(
            "admin",
            "ورود و خروج",
            ActivityActions.Login,
            entityLabel: null,
            changedFields: [],
            succeeded: false,
            statusCode: 401);

        Assert.Contains("ناموفق", sentence);
        Assert.Contains("401", sentence);
        Assert.DoesNotContain("رمز", sentence);
        Assert.DoesNotContain("password", sentence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Describe_ShouldMarkFailedOperationsAsUnsuccessful()
    {
        var sentence = ActivityLogNarrator.Describe(
            "نیما",
            "منو",
            ActivityActions.Delete,
            "آیتم «لاته»",
            [],
            succeeded: false,
            statusCode: 500);

        Assert.Contains("تلاش کرد", sentence);
        Assert.Contains("ناموفق", sentence);
        Assert.Contains("500", sentence);
    }
}

public class AdminSectionsTests
{
    [Theory]
    [InlineData("/api/admin/orders/5", "orders", "سفارشات")]
    [InlineData("/api/admin/inventory/purchases/10", "inventory-purchases", "خرید مواد")]
    [InlineData("/api/admin/users/roles", "roles", "نقش‌ها")]
    [InlineData("/api/auth/login", "auth", "ورود و خروج")]
    public void Resolve_ShouldMapRoutesToPersianSections(
        string path,
        string expectedId,
        string expectedTitle)
    {
        var (sectionId, sectionTitle) = AdminSections.Resolve(path);

        Assert.Equal(expectedId, sectionId);
        Assert.Equal(expectedTitle, sectionTitle);
    }

    [Fact]
    public void Resolve_ShouldFallbackForUnknownPaths()
    {
        var (sectionId, _) = AdminSections.Resolve("/api/unknown/endpoint");

        Assert.Equal("unknown", sectionId);
    }
}

public class ActivityFieldLabelsTests
{
    [Theory]
    [InlineData("price", "قیمت")]
    [InlineData("customerPhone", "تلفن مشتری")]
    [InlineData("unknownFieldName", "unknownFieldName")]
    public void LabelFor_ShouldReturnPersianLabels(string key, string expected)
    {
        Assert.Equal(expected, ActivityFieldLabels.LabelFor(key));
    }

    [Theory]
    [InlineData("createdAt")]
    [InlineData("lastModifiedBy")]
    [InlineData("accessToken")]
    public void ShouldSkip_ShouldIgnoreTechnicalFields(string key)
    {
        Assert.True(ActivityFieldLabels.ShouldSkip(key));
    }
}

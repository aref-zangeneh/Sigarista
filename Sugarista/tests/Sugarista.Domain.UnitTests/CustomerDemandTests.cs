using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class CustomerDemandTests
{
    [Fact]
    public void NormalizeDemandKey_NormalizesArabicLettersAndExtraSpaces()
    {
        var key1 = CustomerDemand.NormalizeDemandKey("  ماچا   لاته  ");
        var key2 = CustomerDemand.NormalizeDemandKey("ماچا لاته");
        var key3 = CustomerDemand.NormalizeDemandKey("كيك   هويج");
        var key4 = CustomerDemand.NormalizeDemandKey("کیک هویج");

        Assert.Equal(key2, key1);
        Assert.Equal(key4, key3);
    }

    [Fact]
    public void Constructor_EmptyItem_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new CustomerDemand("   "));
    }

    [Fact]
    public void Constructor_NonPositiveCount_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new CustomerDemand("شیر جو دوسر", requestedCount: 0));
    }
}

using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class MenuItemRecipeCardTests
{
    [Fact]
    public void Constructor_NormalizesBulletsAndMetadataCleanly()
    {
        var itemId = Guid.NewGuid();
        var card = new MenuItemRecipeCard(
            menuItemId: itemId,
            cardImageUrl: "  /uploads/images/latte.webp  ",
            bulletPoints: ["  ۴۵ میلی‌لیتر اسپرسو  ", "", "   ", "۱۵۰ میلی‌لیتر شیر سرد"],
            servingTemp: " سرد ",
            glasswareOrPlate: " لیوان ۳۶۰ ",
            prepSeconds: 90,
            baristaTip: " یخ را قبل از اسپرسو بریزید ");

        Assert.Equal(itemId, card.MenuItemId);
        Assert.Equal("/uploads/images/latte.webp", card.CardImageUrl);
        Assert.Equal("سرد", card.ServingTemp);
        Assert.Equal("لیوان ۳۶۰", card.GlasswareOrPlate);
        Assert.Equal(90, card.PrepSeconds);
        Assert.Equal("یخ را قبل از اسپرسو بریزید", card.BaristaTip);

        var bullets = card.GetBulletPoints();
        Assert.Equal(2, bullets.Count);
        Assert.Equal("۴۵ میلی‌لیتر اسپرسو", bullets[0]);
        Assert.Equal("۱۵۰ میلی‌لیتر شیر سرد", bullets[1]);
    }

    [Fact]
    public void Constructor_RejectsEmptyMenuItemIdOrNegativePrepSeconds()
    {
        Assert.Throws<DomainException>(() => new MenuItemRecipeCard(Guid.Empty, null, []));
        Assert.Throws<DomainException>(() => new MenuItemRecipeCard(Guid.NewGuid(), null, [], prepSeconds: -5));
    }

    [Fact]
    public void Update_CapsBulletCountAt25AndClearsZeroPrepSecondsToNull()
    {
        var manyBullets = Enumerable.Range(1, 40).Select(i => $"مرحله {i}").ToList();
        var card = new MenuItemRecipeCard(Guid.NewGuid(), null, manyBullets, prepSeconds: 0);

        Assert.Equal(25, card.GetBulletPoints().Count);
        Assert.Null(card.PrepSeconds);
    }
}

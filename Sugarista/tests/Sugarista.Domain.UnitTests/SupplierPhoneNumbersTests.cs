using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests.SupplierTests;

/// <summary>
/// Suppliers support several phone numbers (with a "+" in the admin form);
/// the legacy single PhoneNumber stays in sync as the first entry so existing
/// readers (exports, ingredient-supplier panels) keep working.
/// </summary>
public class SupplierPhoneNumbersTests
{
    private static Supplier CreateSupplier(
        string phoneNumber = "0912-0000000",
        IReadOnlyCollection<string>? phoneNumbers = null)
    {
        return new Supplier(
            "قهوه پخش البرز",
            phoneNumber,
            "آقای رضایی",
            "یادداشت",
            displayOrder: 1,
            isActive: true,
            phoneNumbers: phoneNumbers);
    }

    [Fact]
    public void MultiplePhones_FirstBecomesPrimaryPhoneNumber()
    {
        var supplier = CreateSupplier(phoneNumbers: ["0912-1111111", "021-88776655"]);

        Assert.Equal(["0912-1111111", "021-88776655"], supplier.PhoneNumbers);
        Assert.Equal("0912-1111111", supplier.PhoneNumber); // legacy readers see the first phone
    }

    [Fact]
    public void Phones_AreTrimmed_AndEmptyAndDuplicatesRemoved()
    {
        var supplier = CreateSupplier(phoneNumbers:
        [
            "  0912-1111111  ",
            "",
            "   ",
            "0912-1111111",
            "021-88776655",
        ]);

        Assert.Equal(["0912-1111111", "021-88776655"], supplier.PhoneNumbers);
        Assert.Equal(2, supplier.PhoneNumbers.Length);
    }

    [Fact]
    public void MoreThanTenPhones_CappedAtTen()
    {
        var phones = Enumerable.Range(1, 15).Select(i => $"0912-{i:0000000}").ToList();

        var supplier = CreateSupplier(phoneNumbers: phones);

        Assert.Equal(10, supplier.PhoneNumbers.Length);
        Assert.Equal("0912-0000001", supplier.PhoneNumbers[0]);
        Assert.Equal("0912-0000010", supplier.PhoneNumbers[9]);
    }

    [Fact]
    public void Phones_LongerThan40Chars_AreDropped()
    {
        var supplier = CreateSupplier(phoneNumbers:
        [
            new string('9', 41),
            "0912-2222222",
        ]);

        Assert.Equal(["0912-2222222"], supplier.PhoneNumbers);
        Assert.Equal("0912-2222222", supplier.PhoneNumber);
    }

    [Fact]
    public void WithoutPhoneNumbers_LegacySinglePhone_IsKeptAsList()
    {
        var supplier = CreateSupplier(phoneNumber: "0912-3333333");

        Assert.Equal(["0912-3333333"], supplier.PhoneNumbers);
        Assert.Equal("0912-3333333", supplier.PhoneNumber);
    }

    [Fact]
    public void WithoutAnyPhone_ListIsEmpty_AndPrimaryEmpty()
    {
        var supplier = CreateSupplier(phoneNumber: "");

        Assert.Empty(supplier.PhoneNumbers);
        Assert.Equal(string.Empty, supplier.PhoneNumber);
    }

    [Fact]
    public void ExplicitEmptyList_ClearsAllPhones()
    {
        var supplier = CreateSupplier(phoneNumbers: ["0912-1111111"]);

        supplier.Update(
            "قهوه پخش البرز",
            "0912-1111111",
            "آقای رضایی",
            "یادداشت",
            displayOrder: 1,
            isActive: true,
            phoneNumbers: []);

        Assert.Empty(supplier.PhoneNumbers);
        Assert.Equal(string.Empty, supplier.PhoneNumber);
    }

    [Fact]
    public void Update_CanReplacePhoneList_WhileKeepingEverythingElse()
    {
        var supplier = CreateSupplier(phoneNumbers: ["0912-1111111", "021-88776655"]);

        supplier.Update(
            "قهوه پخش البرز",
            "0912-9999999",
            "آقای رضایی",
            "یادداشت",
            displayOrder: 1,
            isActive: true,
            phoneNumbers: ["0912-8888888"]);

        Assert.Equal(["0912-8888888"], supplier.PhoneNumbers);
        Assert.Equal("0912-8888888", supplier.PhoneNumber);
        Assert.Equal("آقای رضایی", supplier.ContactName);
    }

    [Fact]
    public void Update_WithoutPhoneListArg_KeepsLegacySinglePhoneOnly()
    {
        // Legacy call sites (no list argument) still behave exactly as before.
        var supplier = CreateSupplier(phoneNumbers: ["0912-1111111"]);

        supplier.Update(
            "قهوه پخش البرز",
            "021-11223344",
            "آقای رضایی",
            "یادداشت",
            displayOrder: 1,
            isActive: true);

        Assert.Equal(["021-11223344"], supplier.PhoneNumbers);
        Assert.Equal("021-11223344", supplier.PhoneNumber);
    }

    [Fact]
    public void EmptyPersianName_StillThrows()
    {
        Assert.Throws<DomainException>(() => new Supplier(
            "  ",
            "0912-1111111",
            "",
            "",
            displayOrder: 1));
    }
}

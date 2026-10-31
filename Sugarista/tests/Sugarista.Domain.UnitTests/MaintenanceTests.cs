using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class MaintenanceEquipmentTests
{
    [Fact]
    public void Constructor_ShouldTrimNameAndDefaultToActive()
    {
        var equipment = new MaintenanceEquipment("  دستگاه اسپرسو  ", null, null, null, null, null, null);

        Assert.Equal("دستگاه اسپرسو", equipment.PersianName);
        Assert.True(equipment.IsActive);
        Assert.Empty(equipment.Schedules);
        Assert.Empty(equipment.ServiceLogs);
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyName()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceEquipment("   ", null, null, null, null, null, null));
    }

    [Fact]
    public void TakeOutOfService_ShouldDeactivateAndKeepNote()
    {
        var equipment = new MaintenanceEquipment("گریندر", null, null, null, null, null, null);

        equipment.TakeOutOfService("  موتور سوخت  ");

        Assert.False(equipment.IsActive);
        Assert.Equal("موتور سوخت", equipment.OutOfServiceNote);
        Assert.NotNull(equipment.OutOfServiceAt);
    }

    [Fact]
    public void BringBackToService_ShouldClearOutOfServiceState()
    {
        var equipment = new MaintenanceEquipment("گریندر", null, null, null, null, null, null);
        equipment.TakeOutOfService("خرابی");

        equipment.BringBackToService();

        Assert.True(equipment.IsActive);
        Assert.Null(equipment.OutOfServiceNote);
        Assert.Null(equipment.OutOfServiceAt);
    }
}

public class MaintenanceScheduleTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3651)]
    public void Constructor_ShouldRejectOutOfRangeInterval(int intervalDays)
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceSchedule(Guid.NewGuid(), "روغن‌کاری", intervalDays, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void MarkServiced_ShouldAdvanceNextDueAndRecordCompletion()
    {
        var schedule = new MaintenanceSchedule(
            Guid.NewGuid(), "تعویض فیلتر", 30, new DateOnly(2026, 11, 1));
        var servicedOn = new DateOnly(2026, 10, 20);

        schedule.MarkServiced(servicedOn);

        Assert.Equal(new DateOnly(2026, 11, 19), schedule.NextDueDate);
        Assert.Equal(servicedOn, schedule.LastCompletedOn);
    }

    [Fact]
    public void AdvanceToNextCycle_ShouldMoveOneFullInterval()
    {
        var schedule = new MaintenanceSchedule(
            Guid.NewGuid(), "سرویس دوره‌ای", 90, new DateOnly(2026, 11, 1));

        schedule.AdvanceToNextCycle();

        Assert.Equal(new DateOnly(2027, 1, 30), schedule.NextDueDate);
    }

    [Fact]
    public void ReplaceRecipients_ShouldSwapTheWholeList()
    {
        var schedule = new MaintenanceSchedule(
            Guid.NewGuid(), "سرویس", 30, new DateOnly(2026, 11, 1));

        schedule.ReplaceRecipients(
        [
            (MaintenanceChannel.Email, "ops@example.com", "مدیر عملیات"),
            (MaintenanceChannel.Sms, "09121234567", null),
        ]);

        Assert.Equal(2, schedule.Recipients.Count);
        Assert.Contains(schedule.Recipients, item => item.Target == "ops@example.com");
        Assert.Contains(schedule.Recipients, item => item.Target == "09121234567");
    }
}

public class MaintenanceRecipientTests
{
    [Fact]
    public void EmailRecipient_ShouldAcceptValidAddress()
    {
        var recipient = new MaintenanceRecipient(
            Guid.NewGuid(), MaintenanceChannel.Email, "  ops@example.com  ", " مدیر سایت ");

        Assert.Equal("ops@example.com", recipient.Target);
        Assert.Equal("مدیر سایت", recipient.Label);
    }

    [Fact]
    public void EmailRecipient_ShouldRejectAddressWithoutAtSign()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceRecipient(Guid.NewGuid(), MaintenanceChannel.Email, "not-an-email", null));
    }

    [Fact]
    public void SmsRecipient_ShouldNormalizeToDigits()
    {
        var recipient = new MaintenanceRecipient(
            Guid.NewGuid(), MaintenanceChannel.Sms, " 0912-345-6789 ", null);

        Assert.Equal("09123456789", recipient.Target);
    }

    [Fact]
    public void SmsRecipient_ShouldRejectTooShortNumber()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceRecipient(Guid.NewGuid(), MaintenanceChannel.Sms, "123", null));
    }

    [Fact]
    public void Recipient_ShouldRejectUnknownChannel()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceRecipient(Guid.NewGuid(), (MaintenanceChannel)99, "ops@example.com", null));
    }
}

public class MaintenanceDefaultRecipientTests
{
    [Fact]
    public void DefaultRecipient_ShouldNormalizeTargetAndLabel()
    {
        var recipient = new MaintenanceDefaultRecipient(
            MaintenanceChannel.Sms, " 0912 345 6789 ", "  مدیر سایت  ");

        Assert.Equal("09123456789", recipient.Target);
        Assert.Equal("مدیر سایت", recipient.Label);
    }

    [Fact]
    public void DefaultRecipient_ShouldRejectInvalidEmail()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceDefaultRecipient(MaintenanceChannel.Email, "invalid", null));
    }

    [Fact]
    public void Update_ShouldReplaceAllFields()
    {
        var recipient = new MaintenanceDefaultRecipient(
            MaintenanceChannel.Email, "old@example.com", "قدیمی");

        recipient.Update(MaintenanceChannel.Sms, "09120000000", null);

        Assert.Equal(MaintenanceChannel.Sms, recipient.Channel);
        Assert.Equal("09120000000", recipient.Target);
        Assert.Null(recipient.Label);
    }
}

public class MaintenanceServiceLogTests
{
    [Fact]
    public void Constructor_ShouldTrimTextFields()
    {
        var log = new MaintenanceServiceLog(
            Guid.NewGuid(), null, new DateOnly(2026, 11, 1), 250_000m,
            "  تعمیرکار  ", "  تعویض واشر  ");

        Assert.Equal("تعمیرکار", log.PerformedBy);
        Assert.Equal("تعویض واشر", log.Description);
        Assert.Equal(250_000m, log.Cost);
    }

    [Fact]
    public void Constructor_ShouldRejectNegativeCost()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceServiceLog(
                Guid.NewGuid(), null, new DateOnly(2026, 11, 1), -1m, null, null));
    }

    [Fact]
    public void Constructor_ShouldRejectEmptyEquipmentId()
    {
        Assert.Throws<DomainException>(() =>
            new MaintenanceServiceLog(
                Guid.Empty, null, new DateOnly(2026, 11, 1), 0m, null, null));
    }
}

using Sugarista.Domain.Entities;
using Sugarista.Domain.Exceptions;

namespace Sugarista.Domain.UnitTests;

public class StaffTaskTests
{
    [Fact]
    public void DailyTask_RaisesAt2100TehranTime_AndNotBefore()
    {
        var staffId = Guid.NewGuid();
        var task = new StaffTask(
            title: "خاموش کردن دستگاه اسپرسوساز",
            description: "بررسی شیر آب و قطع برق",
            assignedToAdminUserId: staffId,
            category: "بستن شیفت",
            priority: StaffTaskPriority.Critical,
            recurrenceType: StaffTaskRecurrenceType.Daily,
            raiseTimeOfDay: new TimeOnly(21, 0));

        // 20:59 Tehran = 17:29 UTC -> NOT due yet
        var before2100Utc = new DateTimeOffset(2026, 11, 27, 17, 29, 0, TimeSpan.Zero);
        Assert.Null(task.GetDueOccurrenceDateAt(before2100Utc));

        // 21:00 Tehran = 17:30 UTC -> DUE NOW
        var at2100Utc = new DateTimeOffset(2026, 11, 27, 17, 30, 0, TimeSpan.Zero);
        var dueDate = task.GetDueOccurrenceDateAt(at2100Utc);
        Assert.Equal(new DateOnly(2026, 11, 27), dueDate);
    }

    [Fact]
    public void WeeklyTask_RaisesOnlyOnSelectedDaysOfWeek()
    {
        // 2026-11-27 is Friday (DayOfWeek.Friday = 5, bit = 1 << 5 = 32)
        var fridayBit = 1 << (int)DayOfWeek.Friday;
        var saturdayBit = 1 << (int)DayOfWeek.Saturday;

        var task = new StaffTask(
            title: "بک‌واش هفتگی",
            description: null,
            assignedToAdminUserId: Guid.NewGuid(),
            category: "نظافت",
            priority: StaffTaskPriority.High,
            recurrenceType: StaffTaskRecurrenceType.Weekly,
            raiseTimeOfDay: new TimeOnly(20, 0),
            daysOfWeekMask: saturdayBit);

        // Friday 21:00 Tehran -> not scheduled for Friday
        var friday2100Utc = new DateTimeOffset(2026, 11, 27, 17, 30, 0, TimeSpan.Zero);
        Assert.Null(task.GetDueOccurrenceDateAt(friday2100Utc));

        // Saturday 20:05 Tehran -> scheduled & due!
        var saturday2005Utc = new DateTimeOffset(2026, 11, 28, 16, 35, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 28), task.GetDueOccurrenceDateAt(saturday2005Utc));

        Assert.False(task.IsScheduledForTehranDate(new DateOnly(2026, 11, 27)));
        Assert.True((fridayBit & saturdayBit) == 0);
    }

    [Fact]
    public void OneTimeTask_StaysDueEvenOnFollowingDayUntilCompleted()
    {
        var task = new StaffTask(
            title: "تحویل فیلتر تصفیه آب",
            description: null,
            assignedToAdminUserId: Guid.NewGuid(),
            category: "تجهیزات",
            priority: StaffTaskPriority.High,
            recurrenceType: StaffTaskRecurrenceType.OneTime,
            raiseTimeOfDay: new TimeOnly(14, 0),
            oneTimeDate: new DateOnly(2026, 11, 27));

        // Before 14:00 Tehran on Nov 27 (10:20 UTC = 13:50 Tehran)
        var beforeUtc = new DateTimeOffset(2026, 11, 27, 10, 20, 0, TimeSpan.Zero);
        Assert.Null(task.GetDueOccurrenceDateAt(beforeUtc));

        // Next morning Nov 28 09:00 Tehran -> STILL due with original occurrence date!
        var nextMorningUtc = new DateTimeOffset(2026, 11, 28, 5, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 27), task.GetDueOccurrenceDateAt(nextMorningUtc));
    }

    [Fact]
    public void InactiveTask_NeverRaises()
    {
        var task = new StaffTask(
            title: "تسک متوقف",
            description: null,
            assignedToAdminUserId: Guid.NewGuid(),
            category: null,
            priority: StaffTaskPriority.Normal,
            recurrenceType: StaffTaskRecurrenceType.Daily,
            raiseTimeOfDay: new TimeOnly(8, 0),
            isActive: false);

        var noonUtc = new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
        Assert.Null(task.GetDueOccurrenceDateAt(noonUtc));
    }

    [Fact]
    public void StaffTaskCompletion_ComputesDelayMinutesAccurately()
    {
        var scheduled = new DateTimeOffset(2026, 11, 27, 17, 30, 0, TimeSpan.Zero); // 21:00 Tehran
        var completed = scheduled.AddMinutes(27);

        var completion = new StaffTaskCompletion(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 27),
            scheduled,
            completed,
            Guid.NewGuid(),
            "علی باریستا",
            "خاموش شد");

        Assert.Equal(27, completion.DelayMinutes);
        Assert.Equal("خاموش شد", completion.Note);

        // Early completion clamps delay to 0 (never negative)
        var early = new StaffTaskCompletion(
            Guid.NewGuid(),
            new DateOnly(2026, 11, 27),
            scheduled,
            scheduled.AddMinutes(-10),
            Guid.NewGuid(),
            "علی باریستا");
        Assert.Equal(0, early.DelayMinutes);
    }

    [Fact]
    public void Constructor_ValidatesRequiredFields()
    {
        Assert.Throws<DomainException>(() => new StaffTask(
            "", null, Guid.NewGuid(), null, StaffTaskPriority.Normal, StaffTaskRecurrenceType.Daily, new TimeOnly(21, 0)));

        Assert.Throws<DomainException>(() => new StaffTask(
            "تست", null, Guid.Empty, null, StaffTaskPriority.Normal, StaffTaskRecurrenceType.Daily, new TimeOnly(21, 0)));

        Assert.Throws<DomainException>(() => new StaffTask(
            "تست", null, Guid.NewGuid(), null, StaffTaskPriority.Normal, StaffTaskRecurrenceType.Weekly, new TimeOnly(21, 0), daysOfWeekMask: 0));

        Assert.Throws<DomainException>(() => new StaffTask(
            "تست", null, Guid.NewGuid(), null, StaffTaskPriority.Normal, StaffTaskRecurrenceType.OneTime, new TimeOnly(21, 0), oneTimeDate: null));
    }
}

using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.Common;

public sealed class ClassScheduleRulesTests
{
    [Fact]
    public void Validate_WhenScheduleHasMultipleUniqueSlots_ReturnsNoError()
    {
        var schedules = new[]
        {
            new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Monday, SlotNumber = 1, Room = " P.301 " },
            new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Thursday, SlotNumber = 3, Room = null }
        };

        ClassScheduleRules.Validate(schedules).Should().BeNull();
        ClassScheduleRules.Normalize(schedules).Should().BeEquivalentTo(new[]
        {
            new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Monday, SlotNumber = 1, Room = "P.301" },
            new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Thursday, SlotNumber = 3, Room = null }
        }, options => options.WithStrictOrdering());
    }

    [Fact]
    public void Validate_WhenScheduleContainsDuplicateDayAndSlot_ReturnsValidationMessage()
    {
        var schedules = new[]
        {
            new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Tuesday, SlotNumber = 2, Room = "P.301" },
            new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Tuesday, SlotNumber = 2, Room = "P.302" }
        };

        ClassScheduleRules.Validate(schedules).Should().Contain("Duplicate schedule slot");
    }

    [Theory]
    [InlineData(true, true, ClassStatus.Active)]
    [InlineData(true, false, ClassStatus.Draft)]
    [InlineData(false, true, ClassStatus.Draft)]
    public void DetermineOperationalStatus_RequiresLecturerAndSchedule(
        bool hasLecturer,
        bool hasSchedule,
        ClassStatus expectedStatus)
    {
        var lecturerId = hasLecturer ? Guid.NewGuid() : (Guid?)null;
        var scheduleJson = hasSchedule
            ? ClassScheduleRules.Serialize(new[]
            {
                new ClassScheduleSlotDto { DayOfWeek = DayOfWeek.Wednesday, SlotNumber = 2 }
            })
            : ClassScheduleRules.Serialize(Array.Empty<ClassScheduleSlotDto>());

        ClassScheduleRules.DetermineOperationalStatus(lecturerId, scheduleJson).Should().Be(expectedStatus);
    }
}

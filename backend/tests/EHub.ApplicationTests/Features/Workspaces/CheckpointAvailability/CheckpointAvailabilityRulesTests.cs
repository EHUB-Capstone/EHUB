using EHub.Application.Features.Workspaces.CheckpointAvailability;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointAvailability;

public sealed class CheckpointAvailabilityRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MissingDeadline_IsClosed()
    {
        var result = CheckpointAvailabilityRules.Evaluate(null, true, Now);

        result.Status.Should().Be("Closed");
        result.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void ExpiredDeadline_IsClosed()
    {
        var result = CheckpointAvailabilityRules.Evaluate(Schedule(Now.AddHours(-2), Now.AddMinutes(-1)), true, Now);

        result.Status.Should().Be("Closed");
        result.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void InOrderCheckpoint_OpensOnlyAfterPreviousSubmission()
    {
        var schedule = Schedule(Now.AddMinutes(-1), Now.AddDays(1));

        CheckpointAvailabilityRules.Evaluate(schedule, false, Now).Status.Should().Be("Locked");
        CheckpointAvailabilityRules.Evaluate(schedule, true, Now).Status.Should().Be("Open");
    }

    [Fact]
    public void ArchivedCheckpoint_CannotBeSubmitted()
    {
        var result = CheckpointAvailabilityRules.Evaluate(
            Schedule(Now.AddMinutes(-1), Now.AddDays(1), CheckpointStatus.Archived), true, Now);

        result.Status.Should().Be("Archived");
        result.CanSubmit.Should().BeFalse();
    }

    private static Checkpoint Schedule(DateTime openDate, DateTime dueDate, CheckpointStatus status = CheckpointStatus.Open) => new()
    {
        OpenDate = openDate,
        DueDate = dueDate,
        Status = status
    };
}

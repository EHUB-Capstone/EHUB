using EHub.Application.Features.Workspaces.CheckpointSummaries;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Workspaces.CheckpointSummaries;

public sealed class CheckpointSummaryCircuitBreakerTests
{
    private static readonly DateTime Start = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StaysClosedUntilThreeFailuresInARow()
    {
        var breaker = new CheckpointSummaryCircuitBreaker();

        breaker.RecordFailure(Start);
        breaker.RecordFailure(Start);

        breaker.AllowRequest(Start).Should().BeTrue();
        breaker.Snapshot(Start).State.Should().Be(CheckpointCircuitState.Closed);
        breaker.Snapshot(Start).ConsecutiveFailures.Should().Be(2);
    }

    [Fact]
    public void OpensForTwoMinutesAfterThreeFailuresThenAllowsATest()
    {
        var breaker = new CheckpointSummaryCircuitBreaker();
        for (var i = 0; i < CheckpointSummaryCircuitBreaker.FailuresToOpen; i++) breaker.RecordFailure(Start);

        breaker.AllowRequest(Start.AddMinutes(1)).Should().BeFalse();
        breaker.Snapshot(Start.AddMinutes(1)).State.Should().Be(CheckpointCircuitState.Open);
        breaker.Snapshot(Start.AddMinutes(1)).OpenUntilUtc.Should().Be(Start.AddMinutes(2));

        breaker.AllowRequest(Start.AddMinutes(2)).Should().BeTrue();
        breaker.Snapshot(Start.AddMinutes(2)).State.Should().Be(CheckpointCircuitState.HalfOpen);
    }

    [Fact]
    public void AFailedTestOpensItAgainForLongerAndASuccessClosesItForGood()
    {
        var breaker = new CheckpointSummaryCircuitBreaker();
        for (var i = 0; i < CheckpointSummaryCircuitBreaker.FailuresToOpen; i++) breaker.RecordFailure(Start);

        var retry = Start.AddMinutes(2);
        breaker.RecordFailure(retry);
        breaker.Snapshot(retry).OpenUntilUtc.Should().Be(retry.AddMinutes(5));

        var later = retry.AddMinutes(5);
        breaker.RecordFailure(later);
        breaker.Snapshot(later).OpenUntilUtc.Should().Be(later.AddMinutes(15));

        // Never longer than 15 minutes, however long the outage.
        var again = later.AddMinutes(15);
        breaker.RecordFailure(again);
        breaker.Snapshot(again).OpenUntilUtc.Should().Be(again.AddMinutes(15));

        breaker.RecordSuccess();
        breaker.AllowRequest(again).Should().BeTrue();
        breaker.Snapshot(again).State.Should().Be(CheckpointCircuitState.Closed);
        breaker.Snapshot(again).ConsecutiveFailures.Should().Be(0);

        // After a recovery the next outage starts again with the short pause.
        for (var i = 0; i < CheckpointSummaryCircuitBreaker.FailuresToOpen; i++) breaker.RecordFailure(again);
        breaker.Snapshot(again).OpenUntilUtc.Should().Be(again.AddMinutes(2));
    }

    [Fact]
    public void ASuccessBetweenFailuresKeepsItClosed()
    {
        var breaker = new CheckpointSummaryCircuitBreaker();

        breaker.RecordFailure(Start);
        breaker.RecordFailure(Start);
        breaker.RecordSuccess();
        breaker.RecordFailure(Start);
        breaker.RecordFailure(Start);

        breaker.Snapshot(Start).State.Should().Be(CheckpointCircuitState.Closed);
    }
}

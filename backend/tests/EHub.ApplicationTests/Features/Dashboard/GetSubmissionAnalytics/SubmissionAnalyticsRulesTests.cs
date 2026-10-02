using EHub.Application.Features.Dashboard.GetSubmissionAnalytics;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Dashboard.GetSubmissionAnalytics;

public sealed class SubmissionAnalyticsRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AnArtifactCountsAsSubmittedRegardlessOfTheDeadline() =>
        SubmissionAnalyticsRules.Status(true, Now.AddDays(-1), Now).Should().Be("Submitted");

    [Fact]
    public void MissingRequiresTheCurrentDeadlineToHavePassed()
    {
        SubmissionAnalyticsRules.Status(false, null, Now).Should().Be("NotSubmitted");
        SubmissionAnalyticsRules.Status(false, Now.AddMinutes(1), Now).Should().Be("NotSubmitted");
        SubmissionAnalyticsRules.Status(false, Now, Now).Should().Be("NotSubmitted");
        SubmissionAnalyticsRules.Status(false, Now.AddTicks(-1), Now).Should().Be("Missing");
    }

    [Fact]
    public void ReopeningRemovesMissingUntilTheNewDeadline()
    {
        SubmissionAnalyticsRules.Status(false, Now.AddDays(-1), Now).Should().Be("Missing");
        SubmissionAnalyticsRules.Status(false, Now.AddDays(1), Now).Should().Be("NotSubmitted");
    }
}

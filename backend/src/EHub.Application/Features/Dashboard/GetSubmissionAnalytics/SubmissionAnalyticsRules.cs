namespace EHub.Application.Features.Dashboard.GetSubmissionAnalytics;

public static class SubmissionAnalyticsRules
{
    public static string Status(bool hasFileOrLink, DateTime? currentDeadlineUtc, DateTime nowUtc) =>
        hasFileOrLink ? "Submitted" : currentDeadlineUtc.HasValue && nowUtc > currentDeadlineUtc.Value
            ? "Missing" : "NotSubmitted";
}

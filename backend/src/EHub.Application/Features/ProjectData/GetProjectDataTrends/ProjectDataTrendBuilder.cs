using EHub.Contracts.ProjectData;
using EHub.Domain.Enums;

namespace EHub.Application.Features.ProjectData.GetProjectDataTrends;

internal sealed record TrendProject(
    Guid ProjectId,
    string SemesterCode,
    int Year,
    SemesterTerm Term,
    string SubjectCode,
    Guid ClassId,
    string ClassCode,
    bool IsPotential,
    bool IsInternallyFunded,
    bool IsExternallyFunded,
    bool IsAwarded,
    decimal? Score);

/// <summary>Groups scored projects into semester, subject and class rows and adds the change since the previous semester.</summary>
internal static class ProjectDataTrendBuilder
{
    internal static string TermCode(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => "SP",
        SemesterTerm.Summer => "SU",
        _ => "FA",
    };

    private static int TermOrder(SemesterTerm term) => term switch
    {
        SemesterTerm.Spring => 0,
        SemesterTerm.Summer => 1,
        _ => 2,
    };

    internal static ProjectDataTrendsResponse Build(IReadOnlyCollection<TrendProject> projects) => new()
    {
        Semesters = Rows(projects, project => (project.SemesterCode, project.Year, project.Term, null, null), context: _ => string.Empty),
        Subjects = Rows(projects, project => (project.SemesterCode, project.Year, project.Term, project.SubjectCode, null),
            context: project => project.SubjectCode),
        Classes = Rows(projects, project => (project.SemesterCode, project.Year, project.Term, project.SubjectCode, project.ClassId),
            context: project => project.ClassCode),
    };

    private static ProjectDataTrendRowResponse[] Rows(
        IReadOnlyCollection<TrendProject> projects,
        Func<TrendProject, (string Semester, int Year, SemesterTerm Term, string? Subject, Guid? ClassId)> key,
        Func<TrendProject, string> context)
    {
        var rows = projects
            .GroupBy(key)
            .Select(group =>
            {
                var first = group.First();
                var scores = group.Where(project => project.Score.HasValue).Select(project => project.Score!.Value).ToArray();
                return new Row(
                    group.Key.Year,
                    group.Key.Term,
                    context(first).ToUpperInvariant(),
                    new ProjectDataTrendRowResponse
                    {
                        SemesterCode = group.Key.Semester,
                        Year = group.Key.Year,
                        Term = TermCode(group.Key.Term),
                        SubjectCode = group.Key.Subject,
                        ClassId = group.Key.ClassId,
                        ClassCode = group.Key.ClassId.HasValue ? first.ClassCode : null,
                        TotalGroups = group.Count(),
                        GradedGroups = scores.Length,
                        AverageScore = scores.Length == 0 ? null : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero),
                        PotentialGroups = group.Count(project => project.IsPotential),
                        InternallyFundedGroups = group.Count(project => project.IsInternallyFunded),
                        ExternallyFundedGroups = group.Count(project => project.IsExternallyFunded),
                        AwardedGroups = group.Count(project => project.IsAwarded),
                    });
            })
            .OrderBy(row => row.Year)
            .ThenBy(row => TermOrder(row.Term))
            .ThenBy(row => row.Response.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Response.ClassCode, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Each row is compared with the previous semester of the same subject or class code.
        var previousByContext = new Dictionary<string, ProjectDataTrendRowResponse>();
        var result = new List<ProjectDataTrendRowResponse>(rows.Length);
        foreach (var row in rows)
        {
            previousByContext.TryGetValue(row.Context, out var previous);
            var current = row.Response;
            result.Add(previous is null ? current : new ProjectDataTrendRowResponse
            {
                SemesterCode = current.SemesterCode,
                Year = current.Year,
                Term = current.Term,
                SubjectCode = current.SubjectCode,
                ClassId = current.ClassId,
                ClassCode = current.ClassCode,
                TotalGroups = current.TotalGroups,
                GradedGroups = current.GradedGroups,
                AverageScore = current.AverageScore,
                PotentialGroups = current.PotentialGroups,
                InternallyFundedGroups = current.InternallyFundedGroups,
                ExternallyFundedGroups = current.ExternallyFundedGroups,
                AwardedGroups = current.AwardedGroups,
                AverageScoreChange = current.AverageScore.HasValue && previous.AverageScore.HasValue
                    ? current.AverageScore - previous.AverageScore
                    : null,
                PotentialChange = current.PotentialGroups - previous.PotentialGroups,
                InternallyFundedChange = current.InternallyFundedGroups - previous.InternallyFundedGroups,
                ExternallyFundedChange = current.ExternallyFundedGroups - previous.ExternallyFundedGroups,
                AwardedChange = current.AwardedGroups - previous.AwardedGroups,
            });
            previousByContext[row.Context] = current;
        }

        return [.. result];
    }

    private sealed record Row(int Year, SemesterTerm Term, string Context, ProjectDataTrendRowResponse Response);
}

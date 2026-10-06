using EHub.Application.Features.ProjectData.Common;
using EHub.Contracts.Classes;

namespace EHub.Application.Features.Classes.GroupProjectConsistency;

/// <summary>
/// Group and Project must be 1-1: a Group belongs to a single Project and a Project to a single Group.
/// Students without a Group or without a Project cannot contradict the rule and are ignored.
/// Values are compared after trimming and ignoring case.
/// </summary>
public static class GroupProjectConsistencyRules
{
    public static IReadOnlyCollection<GroupProjectWarningDto> Evaluate(
        IEnumerable<(string? Group, string? Project)> students)
    {
        var pairs = students
            .Select(student => (Group: student.Group?.Trim(), Project: student.Project?.Trim()))
            .Where(pair => !string.IsNullOrEmpty(pair.Group) && !string.IsNullOrEmpty(pair.Project))
            .Select(pair => (Group: pair.Group!, Project: pair.Project!))
            .ToArray();

        var warnings = new List<GroupProjectWarningDto>();

        foreach (var (group, projects) in Collect(pairs.Select(pair => (Key: pair.Group, Value: pair.Project))))
        {
            warnings.Add(new GroupProjectWarningDto
            {
                Type = GroupProjectWarningTypes.GroupHasMultipleProjects,
                Subject = group,
                Related = projects,
                Message = $"Group `{group}` is assigned to multiple projects: {Quote(projects)}."
            });
        }

        foreach (var (project, groups) in Collect(pairs.Select(pair => (Key: pair.Project, Value: pair.Group))))
        {
            warnings.Add(new GroupProjectWarningDto
            {
                Type = GroupProjectWarningTypes.ProjectHasMultipleGroups,
                Subject = project,
                Related = groups,
                Message = $"Project `{project}` is assigned to multiple groups: {Quote(groups)}."
            });
        }

        return warnings;
    }

    /// <summary>Keys that map to more than one distinct value, both sides naturally sorted (G2 before G10).</summary>
    private static IEnumerable<(string Key, string[] Values)> Collect(IEnumerable<(string Key, string Value)> pairs) =>
        pairs
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => (
                Key: group.Select(pair => pair.Key).Order(StringComparer.Ordinal).First(),
                Values: group.Select(pair => pair.Value)
                    .Order(StringComparer.Ordinal)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(NaturalStringComparer.Instance)
                    .ToArray()))
            .Where(item => item.Values.Length > 1)
            .OrderBy(item => item.Key, NaturalStringComparer.Instance);

    private static string Quote(IEnumerable<string> values) =>
        string.Join(", ", values.Select(value => $"`{value}`"));
}

using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal sealed record LoadMentor(Guid Id, string Name, string Email, MentorType Type, string? ContractType);

internal sealed record LoadAssignment(Guid MentorProfileId, string SubjectCode);

internal sealed record MentorSubjectLoad(string SubjectCode, int Before, int Added, int Removed = 0);

internal sealed record MentorLoadSummary(
    LoadMentor Mentor,
    IReadOnlyList<MentorSubjectLoad> Subjects,
    int TotalBefore,
    int TotalAfter);

/// <summary>
/// Summarises how many teams each mentor carries per subject before and after a proposed allocation.
/// Every given mentor is listed, including mentors that carry no team at all.
/// </summary>
internal static class MentorLoadSummaryBuilder
{
    public static IReadOnlyList<MentorLoadSummary> Build(
        IReadOnlyCollection<LoadMentor> mentors,
        IReadOnlyCollection<LoadAssignment> existing,
        IReadOnlyCollection<LoadAssignment> proposed,
        IReadOnlyCollection<LoadAssignment>? ended = null)
    {
        var existingByMentor = existing.ToLookup(item => item.MentorProfileId);
        var proposedByMentor = proposed.ToLookup(item => item.MentorProfileId);
        var endedByMentor = (ended ?? []).ToLookup(item => item.MentorProfileId);

        return mentors
            .OrderBy(mentor => mentor.Type)
            .ThenBy(mentor => mentor.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(mentor =>
            {
                var before = existingByMentor[mentor.Id].GroupBy(item => item.SubjectCode).ToDictionary(group => group.Key, group => group.Count());
                var added = proposedByMentor[mentor.Id].GroupBy(item => item.SubjectCode).ToDictionary(group => group.Key, group => group.Count());
                var removed = endedByMentor[mentor.Id].GroupBy(item => item.SubjectCode).ToDictionary(group => group.Key, group => group.Count());
                var subjects = before.Keys.Union(added.Keys).Union(removed.Keys)
                    .OrderBy(code => code, StringComparer.Ordinal)
                    .Select(code => new MentorSubjectLoad(code, before.GetValueOrDefault(code), added.GetValueOrDefault(code), removed.GetValueOrDefault(code)))
                    .ToList();
                var totalBefore = subjects.Sum(item => item.Before);
                return new MentorLoadSummary(mentor, subjects, totalBefore, totalBefore + subjects.Sum(item => item.Added) - subjects.Sum(item => item.Removed));
            })
            .ToList();
    }
}

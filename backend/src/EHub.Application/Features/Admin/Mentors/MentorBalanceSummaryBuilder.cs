using EHub.Contracts.Mentors;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Admin.Mentors;

internal static class MentorBalanceSummaryBuilder
{
    public static IReadOnlyCollection<MentorAllocationBalancePreview> Build(int semesterTeamCount,
        IReadOnlyCollection<AllocationMentor> mentors, IReadOnlyCollection<MentorAllocationMentorLoad> loads,
        AllocationStrategy strategy) => new[] { MentorType.Enterprise, MentorType.Academic }.Select(type =>
    {
        var eligible = mentors.Where(mentor => mentor.Type == type &&
            (strategy != AllocationStrategy.Ai || !mentor.IsTemporary)).ToArray();
        var count = eligible.Length;
        var afterByMentor = loads.ToDictionary(load => load.MentorProfileId, load => load.TotalAfter);
        var after = eligible.Select(mentor => afterByMentor.GetValueOrDefault(mentor.Id)).ToArray();
        return new MentorAllocationBalancePreview
        {
            MentorType = type.ToString(), SemesterTeamCount = semesterTeamCount, EligibleMentorCount = count,
            TargetMinTeams = count == 0 ? 0 : semesterTeamCount / count,
            TargetMaxTeams = count == 0 ? 0 : semesterTeamCount / count + (semesterTeamCount % count == 0 ? 0 : 1),
            ExtraTeamMentorCount = count == 0 ? 0 : semesterTeamCount % count,
            MinLoadAfter = after.Length == 0 ? 0 : after.Min(), MaxLoadAfter = after.Length == 0 ? 0 : after.Max()
        };
    }).ToArray();
}

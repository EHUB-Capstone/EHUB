using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Mentors;

namespace EHub.Application.Features.Admin.Mentors;

internal static class MentorAllocationExplanationBuilder
{
    public static MentorAllocationFitDetails? Details(MentorFitRanking? ranking) => ranking?.Analysis is { } analysis
        ? new MentorAllocationFitDetails
        {
            ProfileScore = analysis.ProfileScore, HasEvidence = analysis.HasEvidence,
            AvailableProfileFields = analysis.AvailableProfileFields.ToArray(), ProjectNeeds = analysis.ProjectNeeds.ToArray(),
            Criteria = analysis.Criteria.Select(item => new MentorAllocationCriterionFit
                { Key = item.Key, Score = item.Score, Evidence = item.Evidence }).ToArray()
        } : null;

    public static IReadOnlyCollection<MentorAllocationAlternative> Alternatives(AllocationProposal proposal,
        IReadOnlyCollection<AllocationMentor> pool,
        IReadOnlyDictionary<Guid, IReadOnlyDictionary<Guid, MentorFitRanking>>? rankings)
    {
        if (rankings is null || !rankings.TryGetValue(proposal.Team.Id, out var scores)) return [];
        return pool.Where(mentor => mentor.Type == proposal.Mentor.Type && !mentor.IsTemporary &&
                mentor.Id != proposal.Mentor.Id && scores.TryGetValue(mentor.Id, out var score) && score.HasEvidence)
            .OrderByDescending(mentor => scores[mentor.Id].RankingValue ?? scores[mentor.Id].Score)
            .ThenBy(mentor => mentor.Id).Take(3)
            .Select(mentor => new MentorAllocationAlternative
            {
                MentorProfileId = mentor.Id, MentorName = mentor.Name, FitScore = scores[mentor.Id].Score,
                HasEvidence = scores[mentor.Id].HasEvidence, Reasons = scores[mentor.Id].Reasons
            }).ToArray();
    }
}

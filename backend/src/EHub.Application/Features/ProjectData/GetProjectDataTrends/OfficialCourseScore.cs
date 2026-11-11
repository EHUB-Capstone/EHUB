using EHub.Application.Features.Rankings.TeamRankings;
using EHub.Domain.Enums;

namespace EHub.Application.Features.ProjectData.GetProjectDataTrends;

/// <summary>A weighted part of a course score: a checkpoint, or a course assessment rubric.</summary>
internal sealed record TrendComponent(Guid? CheckpointId, Guid? RubricId, decimal Weight);

internal sealed record TrendEvaluation(
    Guid Id,
    Guid RubricId,
    Guid? CheckpointId,
    EvaluationStatus Status,
    decimal TotalScore,
    decimal MaxTotalScore,
    DateTime UpdatedAt);

/// <summary>
/// The official 0-10 course score of one group, used for quality trends. It follows the ranking rules: the latest
/// submitted-or-published evaluation of a component decides, and counts only when it is published, so a newer
/// unpublished re-grade hides an older published score. A group is graded only when every weighted component is.
/// </summary>
internal static class OfficialCourseScore
{
    internal const decimal ScoreScale = 10m;

    internal static decimal? Calculate(
        IReadOnlyCollection<TrendComponent> components,
        IReadOnlyCollection<TrendEvaluation> evaluations)
    {
        var weighted = components.Where(component => component.Weight > 0).ToArray();
        if (weighted.Length == 0) return null;

        var scored = new List<TeamRankingComponent>(weighted.Length);
        foreach (var component in weighted)
        {
            var latest = LatestFor(component, evaluations);
            if (latest is null || latest.Status != EvaluationStatus.Published || latest.MaxTotalScore <= 0)
                return null;

            scored.Add(new TeamRankingComponent(
                component.Weight,
                ToScore(latest),
                EvaluationStatus.Published));
        }

        return TeamRankingRules.CalculateCourseTotal(scored);
    }

    /// <summary>
    /// The evaluation that decides a component: the newest submitted-or-published one. A newer unpublished re-grade
    /// therefore hides an older published score. Shared with the project evidence so both show the same version.
    /// </summary>
    internal static TrendEvaluation? LatestFor(TrendComponent component, IReadOnlyCollection<TrendEvaluation> evaluations) =>
        evaluations
            .Where(evaluation => evaluation.Status is EvaluationStatus.Submitted or EvaluationStatus.Published)
            .Where(evaluation => component.CheckpointId.HasValue
                ? evaluation.CheckpointId == component.CheckpointId
                : evaluation.RubricId == component.RubricId)
            .OrderByDescending(evaluation => evaluation.UpdatedAt)
            .ThenByDescending(evaluation => evaluation.Id)
            .FirstOrDefault();

    /// <summary>The evaluation as a 0-10 score; only meaningful when <see cref="TrendEvaluation.MaxTotalScore"/> is positive.</summary>
    internal static decimal ToScore(TrendEvaluation evaluation) =>
        evaluation.TotalScore / evaluation.MaxTotalScore * ScoreScale;
}

using EHub.Domain.Enums;

namespace EHub.Application.Features.Rankings.TeamRankings;

internal static class TeamRankingRules
{
    public static decimal? CalculateCourseTotal(
        IReadOnlyCollection<TeamRankingComponent> components)
    {
        if (components.Count == 0 || components.All(component => !component.Score.HasValue))
        {
            return null;
        }

        var total = components
            .Where(component => component.Score.HasValue)
            .Sum(component => component.Score!.Value * component.Weight / 100m);
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    public static string ResolveStatus(IReadOnlyCollection<TeamRankingComponent> components)
    {
        if (components.Count == 0 || components.Any(component => !component.Score.HasValue))
        {
            return "INCOMPLETE";
        }

        return components.All(component => component.Status == EvaluationStatus.Published)
            ? "PUBLISHED"
            : "READY_TO_PUBLISH";
    }
}

internal sealed record TeamRankingComponent(
    decimal Weight,
    decimal? Score,
    EvaluationStatus? Status);

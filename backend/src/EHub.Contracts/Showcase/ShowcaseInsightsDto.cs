namespace EHub.Contracts.Showcase;

/// <summary>Owner-only engagement snapshot for the most recent fourteen UTC calendar days.</summary>
public sealed class ShowcaseInsightsDto
{
    public int ViewCount { get; init; }
    public int UpvoteCount { get; init; }
    public int CommentCount { get; init; }
    public IReadOnlyCollection<ShowcaseInsightsDayDto> Daily { get; init; } = Array.Empty<ShowcaseInsightsDayDto>();

    /// <summary>Totals of the fourteen UTC days before <see cref="Daily"/>, to show the trend.</summary>
    public ShowcaseInsightsPeriodDto Previous { get; init; } = new();
}

public sealed class ShowcaseInsightsPeriodDto
{
    public int Views { get; init; }
    public int Upvotes { get; init; }
    public int Comments { get; init; }
}

public sealed class ShowcaseInsightsDayDto
{
    public DateOnly Date { get; init; }
    public int Views { get; init; }
    public int Upvotes { get; init; }
    public int Comments { get; init; }
}

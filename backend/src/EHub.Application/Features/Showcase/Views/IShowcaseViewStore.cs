namespace EHub.Application.Features.Showcase.Views;

public interface IShowcaseViewStore
{
    /// <summary>Records at most one view per viewer and day. Returns null if the showcase is unavailable.</summary>
    Task<int?> RecordAsync(Guid showcaseId, Guid userId, DateOnly dateUtc, CancellationToken cancellationToken);
}

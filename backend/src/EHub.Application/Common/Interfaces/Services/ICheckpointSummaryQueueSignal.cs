namespace EHub.Application.Common.Interfaces.Services;

/// <summary>Wakes the summary worker as soon as a summary is queued instead of waiting for its next poll.</summary>
public interface ICheckpointSummaryQueueSignal
{
    void Signal();

    /// <summary>Returns when signalled or after <paramref name="fallbackDelay"/>; the queue in the database stays the source of truth.</summary>
    Task WaitAsync(TimeSpan fallbackDelay, CancellationToken cancellationToken = default);
}

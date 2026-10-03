namespace EHub.Application.Common.Interfaces.Services;

/// <summary>
/// Provides an in-process hint that new outbox work may be available.
/// Durable polling remains the fallback when a hint is missed or the process restarts.
/// </summary>
public interface IOutboxWakeSignal
{
    void Signal();
    Task WaitAsync(TimeSpan fallbackDelay, CancellationToken cancellationToken = default);
}

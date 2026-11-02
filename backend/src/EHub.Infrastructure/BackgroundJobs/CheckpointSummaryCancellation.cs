using System.Collections.Concurrent;
using EHub.Application.Common.Interfaces.Services;

namespace EHub.Infrastructure.BackgroundJobs;

public sealed class CheckpointSummaryCancellation : ICheckpointSummaryCancellation
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public IDisposable Register(Guid summaryId, out CancellationToken cancellationToken)
    {
        var source = new CancellationTokenSource();
        _running[summaryId] = source;
        cancellationToken = source.Token;
        return new Registration(this, summaryId, source);
    }

    public void Cancel(Guid summaryId)
    {
        if (!_running.TryGetValue(summaryId, out var source)) return;
        try { source.Cancel(); }
        catch (ObjectDisposedException) { /* The run ended between the lookup and the cancel. */ }
    }

    private sealed class Registration(CheckpointSummaryCancellation owner, Guid summaryId, CancellationTokenSource source) : IDisposable
    {
        public void Dispose()
        {
            owner._running.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(summaryId, source));
            source.Dispose();
        }
    }
}

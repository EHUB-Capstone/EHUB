using System.Threading.Channels;
using EHub.Application.Common.Interfaces.Services;

namespace EHub.Infrastructure.BackgroundJobs;

internal sealed class OutboxWakeSignal : IOutboxWakeSignal
{
    private readonly Channel<bool> _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false
    });

    public void Signal() => _signals.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan fallbackDelay, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(fallbackDelay);
        try
        {
            await _signals.Reader.ReadAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The fallback delay elapsed. The worker will poll the durable outbox.
        }
    }
}

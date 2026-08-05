using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Services;

public interface IOutboxEventDispatcher
{
    // Dispatchers must use EventId as their idempotency key because delivery is at-least-once.
    Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

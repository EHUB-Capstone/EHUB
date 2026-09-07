using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Teams;
using Microsoft.Extensions.Logging;

namespace EHub.Infrastructure.Services;

public sealed class ProjectDirectionRealtimeService(
    ILogger<ProjectDirectionRealtimeService> logger) : IProjectDirectionRealtimePublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Guid, Connection>> _connections = new();

    public async Task ListenAsync(Guid userId, WebSocket socket, CancellationToken cancellationToken)
    {
        var connectionId = Guid.NewGuid();
        var connection = new Connection(socket);
        var userConnections = _connections.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Connection>());
        userConnections[connectionId] = connection;
        var buffer = new byte[1024];

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Request aborted or application stopping.
        }
        catch (WebSocketException exception)
        {
            logger.LogDebug(exception, "Project direction realtime connection ended unexpectedly");
        }
        finally
        {
            userConnections.TryRemove(connectionId, out _);
            if (userConnections.IsEmpty) _connections.TryRemove(userId, out _);
            await connection.DisposeAsync();
        }
    }

    public async Task PublishAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        string eventType,
        Guid classId,
        Guid teamId,
        ProjectDirectionDto direction,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            eventType,
            classId,
            teamId,
            direction
        }, JsonOptions);
        await PublishPayloadAsync(recipientUserIds, payload);
    }

    public async Task PublishNotificationReadyAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        Guid classId,
        Guid teamId,
        CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            eventType = "ProjectDirectionNotificationReady",
            classId,
            teamId
        }, JsonOptions);
        await PublishPayloadAsync(recipientUserIds, payload);
    }

    private async Task PublishPayloadAsync(IReadOnlyCollection<Guid> recipientUserIds, byte[] payload)
    {
        foreach (var userId in recipientUserIds.Distinct())
        {
            if (!_connections.TryGetValue(userId, out var userConnections)) continue;
            foreach (var pair in userConnections.ToArray())
            {
                try
                {
                    // Realtime delivery is best-effort and must never turn a committed business action into an API failure.
                    if (await pair.Value.TrySendAsync(payload, CancellationToken.None)) continue;
                    userConnections.TryRemove(pair.Key, out _);
                    pair.Value.Abort();
                }
                catch (Exception exception)
                {
                    userConnections.TryRemove(pair.Key, out _);
                    pair.Value.Abort();
                    logger.LogDebug(exception, "Unable to deliver a project direction realtime event");
                }
            }
        }
    }

    private sealed class Connection(WebSocket socket) : IAsyncDisposable
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private int _disposed;

        public async Task<bool> TrySendAsync(byte[] payload, CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref _disposed) != 0 || socket.State != WebSocketState.Open) return false;
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (Volatile.Read(ref _disposed) != 0 || socket.State != WebSocketState.Open) return false;
                await socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Text, true, cancellationToken);
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (WebSocketException)
            {
                return false;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public void Abort()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            socket.Abort();
            socket.Dispose();
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None);
            }
            catch (Exception exception) when (exception is WebSocketException or ObjectDisposedException)
            {
                // The peer may already be disconnected.
            }
            finally
            {
                socket.Dispose();
            }
        }
    }
}

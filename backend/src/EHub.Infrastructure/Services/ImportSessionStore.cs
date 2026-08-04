using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;

namespace EHub.Infrastructure.Services;

public sealed class ImportSessionStore : IImportSessionStore
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();

    public void SaveSession(Guid sessionId, Guid classId, Guid userId, List<ImportStudentRowPreviewDto> validRows)
    {
        CleanupExpiredSessions();
        var now = DateTime.UtcNow;
        _sessions[sessionId] = new SessionEntry(
            classId,
            userId,
            validRows.ToArray(),
            now.Add(SessionLifetime),
            ImportSessionState.Available);
    }

    public ImportSessionAcquireResult TryAcquireSession(Guid sessionId, Guid classId, Guid userId)
    {
        CleanupExpiredSessions();

        while (_sessions.TryGetValue(sessionId, out var entry))
        {
            if (entry.ExpiresAtUtc <= DateTime.UtcNow || entry.State == ImportSessionState.Consumed)
            {
                return new ImportSessionAcquireResult(ImportSessionAcquireStatus.NotFoundExpiredOrConsumed);
            }

            if (entry.UserId != userId)
            {
                return new ImportSessionAcquireResult(ImportSessionAcquireStatus.UserMismatch);
            }

            if (entry.ClassId != classId)
            {
                return new ImportSessionAcquireResult(ImportSessionAcquireStatus.ClassMismatch);
            }

            if (entry.State == ImportSessionState.Processing)
            {
                return new ImportSessionAcquireResult(ImportSessionAcquireStatus.AlreadyProcessing);
            }

            var acquired = entry with { State = ImportSessionState.Processing };
            if (_sessions.TryUpdate(sessionId, acquired, entry))
            {
                return new ImportSessionAcquireResult(
                    ImportSessionAcquireStatus.Acquired,
                    new ImportSessionData(
                        sessionId,
                        acquired.ClassId,
                        acquired.UserId,
                        acquired.ValidRows,
                        acquired.ExpiresAtUtc));
            }
        }

        return new ImportSessionAcquireResult(ImportSessionAcquireStatus.NotFoundExpiredOrConsumed);
    }

    public void CompleteSession(Guid sessionId)
    {
        ChangeState(sessionId, ImportSessionState.Processing, ImportSessionState.Consumed);
    }

    public void ReleaseSession(Guid sessionId)
    {
        ChangeState(sessionId, ImportSessionState.Processing, ImportSessionState.Available);
    }

    private void ChangeState(Guid sessionId, ImportSessionState expected, ImportSessionState next)
    {
        while (_sessions.TryGetValue(sessionId, out var entry))
        {
            if (entry.State != expected)
            {
                return;
            }

            if (_sessions.TryUpdate(sessionId, entry with { State = next }, entry))
            {
                return;
            }
        }
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var pair in _sessions)
        {
            if (pair.Value.ExpiresAtUtc <= now)
            {
                _sessions.TryRemove(pair.Key, out _);
            }
        }
    }

    private enum ImportSessionState
    {
        Available,
        Processing,
        Consumed
    }

    private sealed record SessionEntry(
        Guid ClassId,
        Guid UserId,
        IReadOnlyCollection<ImportStudentRowPreviewDto> ValidRows,
        DateTime ExpiresAtUtc,
        ImportSessionState State);
}

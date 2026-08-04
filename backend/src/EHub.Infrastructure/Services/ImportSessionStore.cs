using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;

namespace EHub.Infrastructure.Services;

public sealed class ImportSessionStore : IImportSessionStore
{
    private sealed record SessionEntry(Guid ClassId, Guid UserId, List<ImportStudentRowPreviewDto> ValidRows, DateTime CreatedAtUtc);

    private readonly ConcurrentDictionary<Guid, SessionEntry> _sessions = new();

    public void SaveSession(Guid sessionId, Guid classId, Guid userId, List<ImportStudentRowPreviewDto> validRows)
    {
        CleanupExpiredSessions();
        _sessions[sessionId] = new SessionEntry(classId, userId, validRows, DateTime.UtcNow);
    }

    public (Guid ClassId, Guid UserId, List<ImportStudentRowPreviewDto> ValidRows)? GetAndConsumeSession(Guid sessionId)
    {
        CleanupExpiredSessions();

        if (_sessions.TryRemove(sessionId, out var entry))
        {
            if (entry.CreatedAtUtc.AddMinutes(30) >= DateTime.UtcNow)
            {
                return (entry.ClassId, entry.UserId, entry.ValidRows);
            }
        }

        return null;
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _sessions.Keys)
        {
            if (_sessions.TryGetValue(key, out var entry) && entry.CreatedAtUtc.AddMinutes(30) < now)
            {
                _sessions.TryRemove(key, out _);
            }
        }
    }
}

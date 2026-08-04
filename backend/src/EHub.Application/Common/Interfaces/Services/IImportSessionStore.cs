using System;
using System.Collections.Generic;
using EHub.Contracts.Classes;

namespace EHub.Application.Common.Interfaces.Services;

public enum ImportSessionAcquireStatus
{
    Acquired,
    NotFoundExpiredOrConsumed,
    UserMismatch,
    ClassMismatch,
    AlreadyProcessing
}

public sealed record ImportSessionData(
    Guid SessionId,
    Guid ClassId,
    Guid UserId,
    IReadOnlyCollection<ImportStudentRowPreviewDto> ValidRows,
    DateTime ExpiresAtUtc);

public sealed record ImportSessionAcquireResult(
    ImportSessionAcquireStatus Status,
    ImportSessionData? Session = null);

public interface IImportSessionStore
{
    void SaveSession(Guid sessionId, Guid classId, Guid userId, List<ImportStudentRowPreviewDto> validRows);
    ImportSessionAcquireResult TryAcquireSession(Guid sessionId, Guid classId, Guid userId);
    void CompleteSession(Guid sessionId);
    void ReleaseSession(Guid sessionId);
}

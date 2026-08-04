using System;
using System.Collections.Generic;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;
using EHub.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace EHub.UnitTests.Services;

public sealed class ImportSessionStoreTests
{
    private readonly ImportSessionStore _store = new();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _classId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void TryAcquireSession_WhenUserDoesNotOwnSession_DoesNotExposeOrConsumeSession()
    {
        SaveSession();

        var rejected = _store.TryAcquireSession(_sessionId, _classId, Guid.NewGuid());
        var acquiredByOwner = _store.TryAcquireSession(_sessionId, _classId, _userId);

        rejected.Status.Should().Be(ImportSessionAcquireStatus.UserMismatch);
        rejected.Session.Should().BeNull();
        acquiredByOwner.Status.Should().Be(ImportSessionAcquireStatus.Acquired);
        acquiredByOwner.Session!.UserId.Should().Be(_userId);
    }

    [Fact]
    public void TryAcquireSession_WhenClassDoesNotMatch_DoesNotConsumeSession()
    {
        SaveSession();

        var rejected = _store.TryAcquireSession(_sessionId, Guid.NewGuid(), _userId);
        var acquiredForTargetClass = _store.TryAcquireSession(_sessionId, _classId, _userId);

        rejected.Status.Should().Be(ImportSessionAcquireStatus.ClassMismatch);
        acquiredForTargetClass.Status.Should().Be(ImportSessionAcquireStatus.Acquired);
    }

    [Fact]
    public void TryAcquireSession_WhenAlreadyProcessing_RejectsSecondCommit()
    {
        SaveSession();

        var first = _store.TryAcquireSession(_sessionId, _classId, _userId);
        var second = _store.TryAcquireSession(_sessionId, _classId, _userId);

        first.Status.Should().Be(ImportSessionAcquireStatus.Acquired);
        second.Status.Should().Be(ImportSessionAcquireStatus.AlreadyProcessing);
    }

    [Fact]
    public void ReleaseSession_AllowsSafeRetryAfterFailure()
    {
        SaveSession();
        _store.TryAcquireSession(_sessionId, _classId, _userId);

        _store.ReleaseSession(_sessionId);
        var retry = _store.TryAcquireSession(_sessionId, _classId, _userId);

        retry.Status.Should().Be(ImportSessionAcquireStatus.Acquired);
    }

    [Fact]
    public void CompleteSession_PreventsSessionReuse()
    {
        SaveSession();
        _store.TryAcquireSession(_sessionId, _classId, _userId);

        _store.CompleteSession(_sessionId);
        var retry = _store.TryAcquireSession(_sessionId, _classId, _userId);

        retry.Status.Should().Be(ImportSessionAcquireStatus.NotFoundExpiredOrConsumed);
    }

    private void SaveSession()
    {
        _store.SaveSession(_sessionId, _classId, _userId, new List<ImportStudentRowPreviewDto>
        {
            new()
            {
                RowNumber = 2,
                StudentCode = "SE170001",
                FullName = "Safety Test Student",
                Email = "student@example.com",
                MajorCode = "BIT_SE",
                IsValid = true
            }
        });
    }
}

using EHub.Domain.Enums;
using EHub.Infrastructure.BackgroundJobs;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EHub.IntegrationTests.Classes;

// Cleanup of direct-upload sessions that were never completed.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static SubmissionUploadSessionCleaner CreateCleaner(UploadFixture fixture) =>
        new(fixture.Context, fixture.Storage, NullLogger.Instance);

    private static Task<SubmissionUploadSessionStatus> StatusOfAsync(UploadFixture fixture, Guid uploadId) =>
        fixture.Context.SubmissionUploadSessions.AsNoTracking()
            .Where(item => item.Id == uploadId).Select(item => item.Status).SingleAsync();

    [Fact]
    public async Task UploadCleanup_ExpiresAbandonedSessionsAndDeletesTheirObjectsOnly()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var abandoned = (await fixture.InitiateAsync("abandoned.pdf")).Value;
        fixture.PutObject(abandoned.UploadId, "%PDF-1.7 content");
        var neverUploaded = (await fixture.InitiateAsync("never.pdf")).Value;
        var completed = (await fixture.InitiateAsync("done.pdf")).Value;
        fixture.PutObject(completed.UploadId, "%PDF-1.7 content");
        (await fixture.CompleteAsync(completed.UploadId)).IsSuccess.Should().BeTrue();
        var completedKey = fixture.ObjectKeyOf(completed.UploadId);
        var abandonedKey = fixture.ObjectKeyOf(abandoned.UploadId);

        var later = fixture.Clock.UtcNow
            .Add(SubmissionFileLimits.UploadSessionLifetime).Add(SubmissionFileLimits.UploadCleanupGrace).AddMinutes(1);
        var expired = await CreateCleaner(fixture).ExpireAbandonedSessionsAsync(later, CancellationToken.None);

        // Other tests share this database, so only this test's own sessions are asserted.
        expired.Should().BeGreaterThanOrEqualTo(2);
        (await StatusOfAsync(fixture, abandoned.UploadId)).Should().Be(SubmissionUploadSessionStatus.Expired);
        (await StatusOfAsync(fixture, neverUploaded.UploadId)).Should().Be(SubmissionUploadSessionStatus.Expired);
        fixture.Storage.Contains(abandonedKey).Should().BeFalse();
        // A completed upload is a real submitted file: neither its session nor its object may be touched.
        (await StatusOfAsync(fixture, completed.UploadId)).Should().Be(SubmissionUploadSessionStatus.Completed);
        fixture.Storage.Contains(completedKey).Should().BeTrue();
        (await fixture.Context.SubmissionFiles.AnyAsync(item => item.StorageKey == completedKey)).Should().BeTrue();
    }

    [Fact]
    public async Task UploadCleanup_WaitsForTheGracePeriodAndIsIdempotent()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("late.pdf")).Value;
        fixture.PutObject(session.UploadId, "%PDF-1.7 content");
        var cleaner = CreateCleaner(fixture);
        var expiresAt = session.SessionExpiresAt;

        await cleaner.ExpireAbandonedSessionsAsync(expiresAt.AddMinutes(-1), CancellationToken.None);
        await cleaner.ExpireAbandonedSessionsAsync(expiresAt.AddMinutes(5), CancellationToken.None);
        fixture.Storage.Contains(fixture.ObjectKeyOf(session.UploadId)).Should().BeTrue();
        (await StatusOfAsync(fixture, session.UploadId)).Should().Be(SubmissionUploadSessionStatus.Pending);

        var afterGrace = expiresAt.Add(SubmissionFileLimits.UploadCleanupGrace).AddMinutes(1);
        await cleaner.ExpireAbandonedSessionsAsync(afterGrace, CancellationToken.None);
        (await StatusOfAsync(fixture, session.UploadId)).Should().Be(SubmissionUploadSessionStatus.Expired);
        fixture.Storage.Contains(fixture.ObjectKeyOf(session.UploadId)).Should().BeFalse();
        // A second run finds nothing left for this session and changes nothing.
        await cleaner.ExpireAbandonedSessionsAsync(afterGrace, CancellationToken.None);
        (await StatusOfAsync(fixture, session.UploadId)).Should().Be(SubmissionUploadSessionStatus.Expired);
    }

    [Fact]
    public async Task UploadCleanup_RetriesWhenTheObjectCannotBeDeletedYet()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("stuck.pdf")).Value;
        var key = fixture.ObjectKeyOf(session.UploadId);
        fixture.PutObject(session.UploadId, "%PDF-1.7 content");
        fixture.Storage.FailDeletesFor(key);
        var cleaner = CreateCleaner(fixture);
        var afterGrace = session.SessionExpiresAt.Add(SubmissionFileLimits.UploadCleanupGrace).AddMinutes(1);

        await cleaner.ExpireAbandonedSessionsAsync(afterGrace, CancellationToken.None);
        (await StatusOfAsync(fixture, session.UploadId)).Should().Be(SubmissionUploadSessionStatus.Pending);

        fixture.Storage.AllowDeletes();
        await cleaner.ExpireAbandonedSessionsAsync(afterGrace, CancellationToken.None);
        (await StatusOfAsync(fixture, session.UploadId)).Should().Be(SubmissionUploadSessionStatus.Expired);
        fixture.Storage.Contains(key).Should().BeFalse();
    }

    [Fact]
    public async Task UploadCleanup_PurgesOnlyOldFinishedSessions()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var finished = (await fixture.InitiateAsync("finished.pdf")).Value;
        fixture.PutObject(finished.UploadId, "%PDF-1.7 content");
        (await fixture.CompleteAsync(finished.UploadId)).IsSuccess.Should().BeTrue();
        var stillPending = (await fixture.InitiateAsync("pending.pdf")).Value;
        var cleaner = CreateCleaner(fixture);

        var withinRetention = fixture.Clock.UtcNow.Add(SubmissionFileLimits.UploadSessionRetention).AddHours(-1);
        await cleaner.PurgeFinishedSessionsAsync(withinRetention, CancellationToken.None);
        (await fixture.Context.SubmissionUploadSessions.AnyAsync(item => item.Id == finished.UploadId)).Should().BeTrue();

        var pastRetention = fixture.Clock.UtcNow.Add(SubmissionFileLimits.UploadSessionRetention).AddHours(1);
        (await cleaner.PurgeFinishedSessionsAsync(pastRetention, CancellationToken.None)).Should().BeGreaterThanOrEqualTo(1);

        (await fixture.Context.SubmissionUploadSessions.AnyAsync(item => item.Id == finished.UploadId)).Should().BeFalse();
        // Pending sessions are never purged; they must go through expiry so their object is removed first.
        (await fixture.Context.SubmissionUploadSessions.AnyAsync(item => item.Id == stillPending.UploadId)).Should().BeTrue();
    }
}

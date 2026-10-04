using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Checkpoints.LecturerManagement;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Contracts.Checkpoints;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace EHub.IntegrationTests.Classes;

// Direct browser -> R2 upload flow (initiate / complete). Shares seeding helpers with the team workflow tests.
public sealed partial class TeamWorkflowIntegrationTests
{
    private const long OneHundredMegabytes = 100L * 1024 * 1024;

    [Fact]
    public async Task DirectUploadInitiate_Accepts100MbAndReturnsPresignedPutWithoutCreatingFiles()
    {
        using var fixture = await CreateUploadFixtureAsync();

        var result = await fixture.InitiateAsync("report.pdf", size: OneHundredMegabytes);

        result.IsSuccess.Should().BeTrue();
        result.Value.MaxFileSize.Should().Be(OneHundredMegabytes);
        result.Value.Method.Should().Be("PUT");
        result.Value.Headers["Content-Type"].Should().Be("application/pdf");
        result.Value.UrlExpiresAt.Should().Be(fixture.Clock.UtcNow.Add(SubmissionFileLimits.PresignedUploadLifetime));
        result.Value.UploadUrl.Should().Contain($"submissions/{fixture.Seed.TeamId!.Value:N}/checkpoint-1/");
        var session = await fixture.Context.SubmissionUploadSessions.AsNoTracking().SingleAsync(item => item.Id == result.Value.UploadId);
        session.Status.Should().Be(SubmissionUploadSessionStatus.Pending);
        session.DeclaredSize.Should().Be(OneHundredMegabytes);
        (await fixture.Context.SubmissionFiles.AnyAsync(item => item.StorageKey == session.ObjectKey)).Should().BeFalse();
    }

    [Theory]
    [InlineData("big.pdf", OneHundredMegabytes + 1, "100 MB")]
    [InlineData("empty.pdf", 0L, "empty")]
    [InlineData("notes.txt", 10L, "PDF, DOCX, and PPTX")]
    [InlineData("script.exe", 10L, "PDF, DOCX, and PPTX")]
    public async Task DirectUploadInitiate_RejectsOversizeEmptyAndUnsupportedFiles(string name, long size, string expectedMessage)
    {
        using var fixture = await CreateUploadFixtureAsync();

        var result = await fixture.InitiateAsync(name, size: size);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.WorkspaceValidationError);
        result.Error.Message.Should().Contain(expectedMessage);
        (await fixture.Context.SubmissionUploadSessions.AnyAsync(item => item.OriginalName == name)).Should().BeFalse();
    }

    [Fact]
    public async Task DirectUploadInitiate_RejectsMimeTypeThatContradictsTheExtension()
    {
        using var fixture = await CreateUploadFixtureAsync();

        var mismatch = await fixture.InitiateAsync("report.pdf", contentType: "image/png");
        var generic = await fixture.InitiateAsync("report.pdf", contentType: "application/octet-stream");

        mismatch.IsFailure.Should().BeTrue();
        generic.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DirectUploadInitiate_EnforcesRoleMembershipAndCheckpointWindow()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");

        (await fixture.InitiateAsync("a.pdf", userId: fixture.Seed.MentorUserId, role: SystemRoles.Mentor)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await fixture.InitiateAsync("a.pdf", userId: fixture.Seed.LecturerId, role: SystemRoles.Lecturer)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await fixture.InitiateAsync("a.pdf", userId: outsider.Id)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        // Knowing another team's id grants nothing: the proposer is not a member of the second team.
        (await fixture.InitiateAsync("a.pdf", teamId: fixture.Seed.OtherTeamId!.Value)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);

        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(2);
        var closed = await fixture.InitiateAsync("a.pdf");
        closed.IsFailure.Should().BeTrue();
        closed.Error.Code.Should().Be(ErrorCodes.WorkspaceCheckpointNotOpen);
        (await fixture.Context.SubmissionUploadSessions.AnyAsync(item => item.TeamId == fixture.Seed.TeamId)).Should().BeFalse();
    }

    [Fact]
    public async Task DirectUploadInitiate_LimitsPendingSessionsPerUser()
    {
        using var fixture = await CreateUploadFixtureAsync();
        for (var index = 0; index < SubmissionFileLimits.MaxPendingUploadSessionsPerUserTeam; index++)
        {
            (await fixture.InitiateAsync($"file-{index}.pdf")).IsSuccess.Should().BeTrue();
        }

        var rejected = await fixture.InitiateAsync("one-too-many.pdf");

        rejected.IsFailure.Should().BeTrue();
        rejected.Error.Code.Should().Be(ErrorCodes.WorkspaceUploadTooManyPending);

        // Expired sessions no longer count toward the cap.
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.Add(SubmissionFileLimits.UploadSessionLifetime).AddMinutes(1);
        await fixture.ReopenCheckpointAsync();
        (await fixture.InitiateAsync("after-expiry.pdf")).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DirectUploadComplete_CreatesSubmissionFileOnlyAfterTheObjectExists()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("final.pdf")).Value;

        var early = await fixture.CompleteAsync(session.UploadId);

        early.IsFailure.Should().BeTrue();
        early.Error.Code.Should().Be(ErrorCodes.WorkspaceUploadObjectMissing);
        (await fixture.HasStoredFilesAsync()).Should().BeFalse();
        (await fixture.Context.Submissions.AnyAsync(item => item.TeamId == fixture.Seed.TeamId)).Should().BeFalse();

        fixture.PutObject(session.UploadId, "%PDF-1.7 content");
        var completed = await fixture.CompleteAsync(session.UploadId);

        completed.IsSuccess.Should().BeTrue();
        completed.Value.VersionNumber.Should().Be(1);
        completed.Value.OriginalName.Should().Be("final.pdf");
        var stored = await fixture.Context.SubmissionFiles.AsNoTracking().SingleAsync(item => item.Id == completed.Value.Id);
        stored.StorageProvider.Should().Be(SubmissionStorageProvider.R2);
        stored.StorageKey.Should().Be(fixture.ObjectKeyOf(session.UploadId));
        stored.FileUrl.Should().BeEmpty();
        stored.MimeType.Should().Be("application/pdf");
        stored.FileSize.Should().Be(16);
        var saved = await fixture.Context.SubmissionUploadSessions.AsNoTracking().SingleAsync(item => item.Id == session.UploadId);
        saved.Status.Should().Be(SubmissionUploadSessionStatus.Completed);
        saved.SubmissionFileId.Should().Be(stored.Id);
    }

    [Fact]
    public async Task DirectUploadComplete_Accepts100MbObject()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("large.pdf", size: OneHundredMegabytes)).Value;
        fixture.PutObject(session.UploadId, "%PDF-large", reportedSize: OneHundredMegabytes);

        var completed = await fixture.CompleteAsync(session.UploadId);

        completed.IsSuccess.Should().BeTrue();
        completed.Value.FileSize.Should().Be(OneHundredMegabytes);
    }

    [Fact]
    public async Task DirectUploadComplete_IsIdempotentEvenAfterTheCheckpointCloses()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("final.pdf")).Value;
        fixture.PutObject(session.UploadId, "%PDF-1.7 content");

        var first = await fixture.CompleteAsync(session.UploadId);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddHours(5);
        var retry = await fixture.CompleteAsync(session.UploadId);

        first.IsSuccess.Should().BeTrue();
        retry.IsSuccess.Should().BeTrue();
        retry.Value.Id.Should().Be(first.Value.Id);
        (await fixture.Context.SubmissionFiles.CountAsync(item => item.StorageKey == fixture.ObjectKeyOf(session.UploadId))).Should().Be(1);
        (await fixture.Context.Submissions.CountAsync(item => item.TeamId == fixture.Seed.TeamId)).Should().Be(1);
    }

    [Fact]
    public async Task DirectUploadComplete_ConcurrentRetriesCreateExactlyOneFile()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("final.pdf")).Value;
        fixture.PutObject(session.UploadId, "%PDF-1.7 content");

        async Task<Result<WorkspaceCheckpointFileResponse>> CompleteInOwnScopeAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var ownContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var handler = new CheckpointFileUploadHandler(ownContext, fixture.Storage, fixture.Clock,
                new InitiateCheckpointFileUploadRequestValidator());
            return await handler.CompleteAsync(fixture.Seed.TeamId!.Value, 1, session.UploadId,
                fixture.Seed.ProposerUserId, SystemRoles.Student);
        }

        var results = await Task.WhenAll(CompleteInOwnScopeAsync(), CompleteInOwnScopeAsync());

        results.Should().OnlyContain(result => result.IsSuccess);
        results[0].Value.Id.Should().Be(results[1].Value.Id);
        (await fixture.Context.SubmissionFiles.CountAsync(item => item.StorageKey == fixture.ObjectKeyOf(session.UploadId))).Should().Be(1);
    }

    [Theory]
    [InlineData("size-mismatch")]
    [InlineData("over-limit")]
    [InlineData("wrong-signature")]
    public async Task DirectUploadComplete_RejectsAndDeletesInvalidObjects(string scenario)
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("final.pdf", size: 16)).Value;
        switch (scenario)
        {
            case "size-mismatch": fixture.PutObject(session.UploadId, "%PDF-1.7", reportedSize: 17); break;
            case "over-limit": fixture.PutObject(session.UploadId, "%PDF-1.7 content", reportedSize: OneHundredMegabytes + 1); break;
            default: fixture.PutObject(session.UploadId, "not a pdf at all"); break;
        }

        var result = await fixture.CompleteAsync(session.UploadId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.WorkspaceValidationError);
        fixture.Storage.Deleted.Should().Contain(fixture.ObjectKeyOf(session.UploadId));
        (await fixture.Context.SubmissionFiles.AnyAsync(item => item.StorageKey == fixture.ObjectKeyOf(session.UploadId))).Should().BeFalse();
        (await fixture.Context.Submissions.AnyAsync(item => item.TeamId == fixture.Seed.TeamId)).Should().BeFalse();
        (await fixture.Context.SubmissionUploadSessions.AsNoTracking().SingleAsync(item => item.Id == session.UploadId))
            .Status.Should().Be(SubmissionUploadSessionStatus.Aborted);
    }

    [Fact]
    public async Task DirectUploadComplete_RejectsExpiredSessionsAndClosedCheckpoints()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var expiring = (await fixture.InitiateAsync("late.pdf")).Value;
        fixture.PutObject(expiring.UploadId, "%PDF-1.7 content");

        // Past the session lifetime, even though the checkpoint window is still open.
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(61);
        var expired = await fixture.CompleteAsync(expiring.UploadId);
        expired.Error.Code.Should().Be(ErrorCodes.WorkspaceUploadSessionExpired);

        // A live session cannot be completed once the checkpoint window has closed.
        await fixture.ReopenCheckpointAsync(endsIn: TimeSpan.FromMinutes(5));
        var closing = (await fixture.InitiateAsync("closed.pdf")).Value;
        fixture.PutObject(closing.UploadId, "%PDF-1.7 content");
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(10);
        var closed = await fixture.CompleteAsync(closing.UploadId);
        closed.Error.Code.Should().Be(ErrorCodes.WorkspaceCheckpointNotOpen);
        (await fixture.HasStoredFilesAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DirectUploadComplete_RejectsOtherUsersAndOtherTeams()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var session = (await fixture.InitiateAsync("final.pdf")).Value;
        fixture.PutObject(session.UploadId, "%PDF-1.7 content");
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");

        (await fixture.CompleteAsync(session.UploadId, userId: outsider.Id)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await fixture.CompleteAsync(session.UploadId, userId: fixture.Seed.MentorUserId, role: SystemRoles.Mentor)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await fixture.CompleteAsync(session.UploadId, teamId: fixture.Seed.OtherTeamId!.Value)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await fixture.CompleteAsync(Guid.NewGuid())).Error.Code.Should().Be(ErrorCodes.CommonNotFoundError);
        (await fixture.HasStoredFilesAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task DirectUploadEndpoints_RequireAuthenticationAndStudentMembership()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var teamId = fixture.Seed.TeamId!.Value;
        var tokenService = fixture.Scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var student = await fixture.Context.Users.SingleAsync(item => item.Id == fixture.Seed.ProposerUserId);
        var lecturer = await fixture.Context.Users.SingleAsync(item => item.Id == fixture.Seed.LecturerId);
        using var client = _factory.CreateClient();
        var initiateUrl = $"/api/workspace/checkpoints/teams/{teamId}/checkpoints/1/uploads";
        var completeUrl = $"{initiateUrl}/{Guid.NewGuid()}/complete";
        var body = JsonContent.Create(new { fileName = "report.pdf", contentType = "application/pdf", size = 1024 });

        (await client.PostAsync(initiateUrl, body)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PostAsync(completeUrl, null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var lecturerRequest = new HttpRequestMessage(HttpMethod.Post, initiateUrl) { Content = JsonContent.Create(new { fileName = "report.pdf", size = 1024 }) };
        lecturerRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(lecturer, [SystemRoles.Lecturer]).Token);
        (await client.SendAsync(lecturerRequest)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Real composition: the R2 storage presigns offline with the Testing placeholder credentials.
        using var studentRequest = new HttpRequestMessage(HttpMethod.Post, initiateUrl) { Content = JsonContent.Create(new { fileName = "report.pdf", contentType = "application/pdf", size = 1024 }) };
        studentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(student, [SystemRoles.Student]).Token);
        using var ok = await client.SendAsync(studentRequest);
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await ok.Content.ReadAsStringAsync();
        payload.Should().Contain("testing-account-id.r2.cloudflarestorage.com/testing-bucket/submissions/");
        payload.Should().NotContain("testing-secret-access-key");

        using var tooLarge = new HttpRequestMessage(HttpMethod.Post, initiateUrl) { Content = JsonContent.Create(new { fileName = "report.pdf", size = OneHundredMegabytes + 1 }) };
        tooLarge.Headers.Authorization = studentRequest.Headers.Authorization;
        (await client.SendAsync(tooLarge)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // Drives the real initiate -> PUT -> complete flow with a fake bucket; used by tests that only need a stored file.
    private static async Task<Result<WorkspaceCheckpointFileResponse>> DirectUploadAsync(
        AppDbContext context, IDateTimeProvider clock, FakeObjectStorage storage, Guid teamId, int checkpointNumber, string fileName, Guid userId)
    {
        var handler = new CheckpointFileUploadHandler(context, storage, clock, new InitiateCheckpointFileUploadRequestValidator());
        var content = "%PDF-test"u8.ToArray();
        var initiated = await handler.InitiateAsync(teamId, checkpointNumber,
            new InitiateCheckpointFileUploadRequest { FileName = fileName, Size = content.Length }, userId, SystemRoles.Student);
        if (initiated.IsFailure) return Result.Failure<WorkspaceCheckpointFileResponse>(initiated.Error);
        var key = (await context.SubmissionUploadSessions.AsNoTracking().SingleAsync(item => item.Id == initiated.Value.UploadId)).ObjectKey;
        storage.Put(key, content);
        return await handler.CompleteAsync(teamId, checkpointNumber, initiated.Value.UploadId, userId, SystemRoles.Student);
    }

    private async Task<UploadFixture> CreateUploadFixtureAsync()
    {
        var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var seed = await CreateSeedAsync(context, createProposal: false, createTeam: true);
        var courseId = await context.Classes.Where(item => item.Id == seed.ClassId).Select(item => item.CourseId).SingleAsync();
        var checkpoint = new Checkpoint
        {
            CourseId = courseId, Name = "Direct upload checkpoint", CheckpointNumber = 1,
            Status = CheckpointStatus.Draft, CreatedById = seed.AdminId
        };
        context.Checkpoints.Add(checkpoint);
        context.Projects.Add(new Project
        {
            TeamId = seed.TeamId!.Value, Name = "Direct upload project",
            Status = ProjectStatus.Draft, CreatedById = seed.ProposerUserId
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var clock = new FixedCheckpointTimeProvider(DateTime.UtcNow);
        var fixture = new UploadFixture(scope, context, seed, clock, checkpoint.Id);
        await fixture.ReopenCheckpointAsync();
        return fixture;
    }

    private sealed class UploadFixture : IDisposable
    {
        private readonly Guid checkpointId;
        private readonly CheckpointFileUploadHandler handler;

        public UploadFixture(IServiceScope scope, AppDbContext context, WorkflowSeed seed, FixedCheckpointTimeProvider clock, Guid checkpointId)
        {
            Scope = scope;
            Context = context;
            Seed = seed;
            Clock = clock;
            this.checkpointId = checkpointId;
            Storage = new FakeObjectStorage();
            handler = new CheckpointFileUploadHandler(context, Storage, clock, new InitiateCheckpointFileUploadRequestValidator());
        }

        public IServiceScope Scope { get; }
        public AppDbContext Context { get; }
        public WorkflowSeed Seed { get; }
        public FixedCheckpointTimeProvider Clock { get; }
        public FakeObjectStorage Storage { get; }

        /// <summary>Opens a window from one hour ago to one hour ahead of the (possibly advanced) fixture clock.</summary>
        public async Task ReopenCheckpointAsync(TimeSpan? endsIn = null)
        {
            var manager = new LecturerCheckpointManagementHandler(Context, Clock);
            var saved = await manager.SaveScheduleAsync(Seed.ClassId, checkpointId, new SaveClassCheckpointScheduleRequest
            {
                StartDateUtc = Clock.UtcNow.AddHours(-1),
                EndDateUtc = Clock.UtcNow.Add(endsIn ?? TimeSpan.FromHours(1))
            }, Seed.LecturerId);
            saved.IsSuccess.Should().BeTrue();
        }

        public Task<Result<CheckpointFileUploadSessionResponse>> InitiateAsync(
            string fileName, long size = 16, string contentType = "", Guid? userId = null, string role = SystemRoles.Student, Guid? teamId = null) =>
            handler.InitiateAsync(teamId ?? Seed.TeamId!.Value, 1,
                new InitiateCheckpointFileUploadRequest { FileName = fileName, ContentType = contentType, Size = size },
                userId ?? Seed.ProposerUserId, role);

        public Task<Result<WorkspaceCheckpointFileResponse>> CompleteAsync(
            Guid uploadId, Guid? userId = null, string role = SystemRoles.Student, Guid? teamId = null) =>
            handler.CompleteAsync(teamId ?? Seed.TeamId!.Value, 1, uploadId, userId ?? Seed.ProposerUserId, role);

        public CheckpointFileHandler CreateFileHandler(IDocumentPreviewConverter? converter = null) =>
            new(Context, new InMemoryCheckpointStorage(), Storage, Clock, converter ?? new SuccessfulPreviewConverter());

        /// <summary>Uploads through initiate/PUT/complete and returns the stored file.</summary>
        public async Task<WorkspaceCheckpointFileResponse> UploadStoredFileAsync(string fileName, byte[] content, long? reportedSize = null)
        {
            var session = await InitiateAsync(fileName, size: reportedSize ?? content.Length);
            session.IsSuccess.Should().BeTrue();
            Storage.Put(ObjectKeyOf(session.Value.UploadId), content, reportedSize);
            var completed = await CompleteAsync(session.Value.UploadId);
            completed.IsSuccess.Should().BeTrue();
            return completed.Value;
        }

        public string StoredKeyOf(Guid fileId) =>
            Context.SubmissionFiles.AsNoTracking().Single(item => item.Id == fileId).StorageKey!;

        /// <summary>Adds a Cloudinary-era file to the same submission as an existing file.</summary>
        public async Task<WorkspaceCheckpointFileResponse> AddLegacyFileAsync(Guid siblingFileId)
        {
            var submissionId = await Context.SubmissionFiles.Where(item => item.Id == siblingFileId).Select(item => item.SubmissionId).SingleAsync();
            var legacy = new SubmissionFile
            {
                SubmissionId = submissionId, VersionNumber = 99, FileName = "legacy.pdf", OriginalName = "legacy.pdf",
                FileUrl = "https://example.test/legacy", CloudinaryPublicId = "legacy-id", MimeType = "application/pdf",
                FileSize = 9, FileType = SubmissionFileType.Report, UploadedById = Seed.ProposerUserId,
                UploadedAt = Clock.UtcNow, CreatedBy = Seed.ProposerUserId
            };
            Context.SubmissionFiles.Add(legacy);
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            return new WorkspaceCheckpointFileResponse { Id = legacy.Id };
        }

        public Task<bool> HasStoredFilesAsync() =>
            Context.SubmissionFiles.AnyAsync(item => item.StorageKey != null && item.Submission.TeamId == Seed.TeamId);

        public string ObjectKeyOf(Guid uploadId) =>
            Context.SubmissionUploadSessions.AsNoTracking().Single(item => item.Id == uploadId).ObjectKey;

        public void PutObject(Guid uploadId, string content, long? reportedSize = null) =>
            Storage.Put(ObjectKeyOf(uploadId), System.Text.Encoding.ASCII.GetBytes(content), reportedSize);

        public void Dispose() => Scope.Dispose();
    }

    private sealed class FakeObjectStorage : ISubmissionObjectStorage
    {
        private readonly Dictionary<string, (byte[] Bytes, long Size)> objects = new(StringComparer.Ordinal);
        public List<string> Deleted { get; } = [];
        public List<string> Uploaded { get; } = [];

        /// <summary>Lets a test report a 100 MB object without allocating 100 MB.</summary>
        public void Put(string key, byte[] bytes, long? reportedSize = null) => objects[key] = (bytes, reportedSize ?? bytes.Length);

        public PresignedObjectUpload CreatePresignedUpload(string objectKey, string contentType, long contentLength, TimeSpan lifetime) =>
            new($"https://r2.test/{objectKey}?expires={(int)lifetime.TotalSeconds}",
                new Dictionary<string, string> { ["Content-Type"] = contentType });

        public Task<Result<StoredObjectInfo>> GetObjectInfoAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(objects.TryGetValue(objectKey, out var stored)
                ? Result.Success(new StoredObjectInfo(stored.Size, null))
                : Result.Failure<StoredObjectInfo>(ErrorCodes.CommonNotFoundError, "missing"));

        public Task<Result<byte[]>> ReadRangeAsync(string objectKey, long offset, int length, CancellationToken cancellationToken = default) =>
            Task.FromResult(objects.TryGetValue(objectKey, out var stored)
                ? Result.Success(stored.Bytes.Skip((int)offset).Take(length).ToArray())
                : Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "missing"));

        public Task<Result<byte[]>> DownloadAsync(string objectKey, CancellationToken cancellationToken = default) =>
            Task.FromResult(objects.TryGetValue(objectKey, out var stored)
                ? Result.Success(stored.Bytes)
                : Result.Failure<byte[]>(ErrorCodes.CommonNotFoundError, "missing"));

        public Task<Result> UploadAsync(string objectKey, byte[] content, string contentType, CancellationToken cancellationToken = default)
        {
            objects[objectKey] = (content, content.Length);
            Uploaded.Add(objectKey);
            return Task.FromResult(Result.Success());
        }

        public string CreatePresignedDownloadUrl(string objectKey, string fileName, string contentType, TimeSpan lifetime) =>
            $"https://r2.test/get/{objectKey}?name={Uri.EscapeDataString(fileName)}&expires={(int)lifetime.TotalSeconds}";

        public string CreatePresignedInlinePdfUrl(string objectKey, TimeSpan lifetime) =>
            $"https://r2.test/inline/{objectKey}?inline=1&expires={(int)lifetime.TotalSeconds}";

        public bool Contains(string objectKey) => objects.ContainsKey(objectKey);

        private readonly HashSet<string> stuckDeletes = new(StringComparer.Ordinal);

        /// <summary>Simulates a storage outage: deletes for this key are accepted but do nothing.</summary>
        public void FailDeletesFor(string objectKey) => stuckDeletes.Add(objectKey);
        public void AllowDeletes() => stuckDeletes.Clear();

        public Task DeleteAsync(string objectKey, CancellationToken cancellationToken = default)
        {
            if (stuckDeletes.Contains(objectKey)) return Task.CompletedTask;
            objects.Remove(objectKey);
            Deleted.Add(objectKey);
            return Task.CompletedTask;
        }
    }
}

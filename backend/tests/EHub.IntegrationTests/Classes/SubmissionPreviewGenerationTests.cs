using EHub.Application.Common.Interfaces.Storage;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Domain.Enums;
using EHub.Infrastructure.BackgroundJobs;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EHub.IntegrationTests.Classes;

// DOCX/PPTX previews are generated in the background right after upload.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static SubmissionPreviewGenerationRunner CreatePreviewRunner(UploadFixture fixture, IDocumentPreviewConverter converter) =>
        new(fixture.Context,
            new CheckpointPreviewGenerator(fixture.Context, new InMemoryCheckpointStorage(), fixture.Storage, fixture.Clock, converter),
            NullLogger.Instance);

    // Other tests share this database and leave queued files behind; clear them so the file under test is the only one left.
    private static async Task DrainPreviewQueueAsync(SubmissionPreviewGenerationRunner runner, DateTime now)
    {
        while (await runner.ProcessNextAsync(now, CancellationToken.None)) { }
    }

    private static Task<EHub.Domain.Entities.SubmissionFile> LoadFileAsync(UploadFixture fixture, Guid fileId) =>
        fixture.Context.SubmissionFiles.AsNoTracking().SingleAsync(item => item.Id == fileId);

    [Fact]
    public async Task PreviewQueue_OnlySmallDocxAndPptxFilesAreQueuedWhenUploadCompletes()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var pptx = await fixture.UploadStoredFileAsync("deck.pptx", BuildOfficeZip("ppt/presentation.xml"));
        var pdf = await fixture.UploadStoredFileAsync("report.pdf", "%PDF-1.7 content"u8.ToArray());
        var huge = await fixture.UploadStoredFileAsync("huge.docx", BuildOfficeZip("word/document.xml"),
            reportedSize: SubmissionFileLimits.MaxPreviewConvertSizeBytes + 1);

        var queued = await LoadFileAsync(fixture, docx.Id);
        queued.PreviewStatus.Should().Be(SubmissionPreviewStatus.Pending);
        queued.PreviewNextAttemptAtUtc.Should().BeCloseTo(fixture.Clock.UtcNow, TimeSpan.FromMilliseconds(1));
        (await LoadFileAsync(fixture, pptx.Id)).PreviewStatus.Should().Be(SubmissionPreviewStatus.Pending);
        (await LoadFileAsync(fixture, pdf.Id)).PreviewStatus.Should().Be(SubmissionPreviewStatus.None);
        (await LoadFileAsync(fixture, huge.Id)).PreviewStatus.Should().Be(SubmissionPreviewStatus.None);
    }

    [Fact]
    public async Task PreviewJob_ConvertsOnceAndALaterPreviewRequestIsServedFromTheCache()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new CountingPreviewConverter();
        var runner = CreatePreviewRunner(fixture, converter);
        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var before = converter.ConversionCount;

        (await runner.ProcessNextAsync(fixture.Clock.UtcNow, CancellationToken.None)).Should().BeTrue();

        converter.ConversionCount.Should().Be(before + 1);
        var ready = await LoadFileAsync(fixture, docx.Id);
        ready.PreviewStatus.Should().Be(SubmissionPreviewStatus.Ready);
        ready.PreviewAttemptCount.Should().Be(0);
        ready.PreviewPdfPublicId.Should().Be($"{ready.StorageKey}.preview.pdf");
        ready.PreviewSourceVersionNumber.Should().Be(ready.VersionNumber);
        fixture.Storage.Contains(ready.PreviewPdfPublicId!).Should().BeTrue();
        // Nothing left to do: the next poll is idle.
        (await runner.ProcessNextAsync(fixture.Clock.UtcNow, CancellationToken.None)).Should().BeFalse();

        var preview = await fixture.CreateFileHandler(converter)
            .PreviewAsync(fixture.Seed.TeamId!.Value, 1, docx.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);

        preview.IsSuccess.Should().BeTrue();
        preview.Value.FromCache.Should().BeTrue();
        preview.Value.Timings!.ConvertMs.Should().Be(0);
        converter.ConversionCount.Should().Be(before + 1);
    }

    [Fact]
    public async Task PreviewJob_AndAPreviewRequestRunningTogetherConvertTheFileOnlyOnce()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new CountingPreviewConverter();
        await DrainPreviewQueueAsync(CreatePreviewRunner(fixture, converter), fixture.Clock.UtcNow);
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var before = converter.ConversionCount;
        var teamId = fixture.Seed.TeamId!.Value;

        async Task RunJobAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var ownContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var runner = new SubmissionPreviewGenerationRunner(ownContext,
                new CheckpointPreviewGenerator(ownContext, new InMemoryCheckpointStorage(), fixture.Storage, fixture.Clock, converter),
                NullLogger.Instance);
            await runner.ProcessNextAsync(fixture.Clock.UtcNow, CancellationToken.None);
        }

        async Task<Result<CheckpointFilePreview>> RequestPreviewAsync()
        {
            using var scope = _factory.Services.CreateScope();
            var ownContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var handler = new CheckpointFileHandler(ownContext, new InMemoryCheckpointStorage(), fixture.Storage, fixture.Clock, converter);
            return await handler.PreviewAsync(teamId, 1, docx.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        }

        var results = await Task.WhenAll(RunJobAsync().ContinueWith(_ => (Result<CheckpointFilePreview>?)null), RequestPreviewAsync().ContinueWith(task => (Result<CheckpointFilePreview>?)task.Result));

        results[1]!.IsSuccess.Should().BeTrue();
        converter.ConversionCount.Should().Be(before + 1);
        (await LoadFileAsync(fixture, docx.Id)).PreviewStatus.Should().Be(SubmissionPreviewStatus.Ready);
    }

    [Fact]
    public async Task PreviewJob_RetriesTransientFailuresWithBackoffThenGivesUp()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new FailingPreviewConverter(ErrorCodes.WorkspaceFilePreviewUnavailable);
        var runner = CreatePreviewRunner(fixture, converter);
        var t0 = fixture.Clock.UtcNow;
        await DrainPreviewQueueAsync(runner, t0);
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));

        await DrainPreviewQueueAsync(runner, t0);
        var first = await LoadFileAsync(fixture, docx.Id);
        first.PreviewStatus.Should().Be(SubmissionPreviewStatus.Pending);
        first.PreviewAttemptCount.Should().Be(1);
        first.PreviewNextAttemptAtUtc.Should().BeCloseTo(t0 + SubmissionFileLimits.PreviewGenerationRetryDelays[0], TimeSpan.FromMilliseconds(1));
        first.PreviewLastError.Should().Contain(ErrorCodes.WorkspaceFilePreviewUnavailable);

        // Not due yet: nothing runs.
        await DrainPreviewQueueAsync(runner, t0.AddSeconds(10));
        converter.Calls.Should().Be(1);

        var t1 = t0.AddSeconds(31);
        await DrainPreviewQueueAsync(runner, t1);
        var second = await LoadFileAsync(fixture, docx.Id);
        second.PreviewAttemptCount.Should().Be(2);
        second.PreviewNextAttemptAtUtc.Should().BeCloseTo(t1 + SubmissionFileLimits.PreviewGenerationRetryDelays[1], TimeSpan.FromMilliseconds(1));

        var t2 = t1.AddMinutes(3);
        await DrainPreviewQueueAsync(runner, t2);
        var last = await LoadFileAsync(fixture, docx.Id);
        last.PreviewStatus.Should().Be(SubmissionPreviewStatus.Failed);
        last.PreviewAttemptCount.Should().Be(SubmissionFileLimits.MaxPreviewGenerationAttempts);
        last.PreviewNextAttemptAtUtc.Should().BeNull();

        // A failed file is not picked up again.
        await DrainPreviewQueueAsync(runner, t2.AddHours(1));
        converter.Calls.Should().Be(3);
    }

    [Fact]
    public async Task PreviewJob_MarksCorruptDocumentsFailedImmediatelyWithoutRetrying()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new CountingPreviewConverter();
        var runner = CreatePreviewRunner(fixture, converter);
        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);
        // Starts like a DOCX (valid ZIP) but has no word/document.xml.
        var fake = await fixture.UploadStoredFileAsync("fake.docx", BuildOfficeZip("unrelated.txt"));
        var before = converter.ConversionCount;

        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);

        var stored = await LoadFileAsync(fixture, fake.Id);
        stored.PreviewStatus.Should().Be(SubmissionPreviewStatus.Failed);
        stored.PreviewAttemptCount.Should().Be(1);
        stored.PreviewLastError.Should().Contain(ErrorCodes.WorkspaceFilePreviewConversionFailed);
        converter.ConversionCount.Should().Be(before);
    }

    [Fact]
    public async Task PreviewJob_IgnoresDeletedFilesAndFilesAlreadyLeasedByAnotherWorker()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new CountingPreviewConverter();
        var runner = CreatePreviewRunner(fixture, converter);
        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);
        var deleted = await fixture.UploadStoredFileAsync("gone.docx", BuildOfficeZip("word/document.xml"));
        var leased = await fixture.UploadStoredFileAsync("leased.docx", BuildOfficeZip("word/document.xml"));
        (await fixture.CreateFileHandler(converter).DeleteAsync(
            fixture.Seed.TeamId!.Value, 1, deleted.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();
        // Another worker claimed the file: its lease keeps it out of the queue until it expires.
        await fixture.Context.SubmissionFiles.Where(item => item.Id == leased.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PreviewNextAttemptAtUtc, fixture.Clock.UtcNow.AddMinutes(5)));
        var before = converter.ConversionCount;

        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);

        converter.ConversionCount.Should().Be(before);
        (await LoadFileAsync(fixture, leased.Id)).PreviewStatus.Should().Be(SubmissionPreviewStatus.Pending);

        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow.AddMinutes(6));
        (await LoadFileAsync(fixture, leased.Id)).PreviewStatus.Should().Be(SubmissionPreviewStatus.Ready);
    }

    private sealed class FailingPreviewConverter(string errorCode) : IDocumentPreviewConverter
    {
        public int Calls { get; private set; }

        public Task<Result<DocumentPreviewConversionResult>> ConvertToPdfAsync(
            byte[] sourceContent, string sourceExtension, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(Result.Failure<DocumentPreviewConversionResult>(errorCode, "Converter unavailable."));
        }
    }
}

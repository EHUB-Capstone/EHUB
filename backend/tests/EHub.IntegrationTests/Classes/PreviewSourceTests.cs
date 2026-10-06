using EHub.Application.Common.Interfaces.Identity;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EHub.IntegrationTests.Classes;

// preview-source: tells the browser where to read the PDF preview; never converts inside the request.
public sealed partial class TeamWorkflowIntegrationTests
{
    private static string PreviewSourceUrl(UploadFixture fixture, Guid fileId, bool retry = false) =>
        $"/api/workspace/checkpoints/teams/{fixture.Seed.TeamId}/checkpoints/1/files/{fileId}/preview-source{(retry ? "?retry=true" : "")}";

    [Fact]
    public async Task PreviewSource_ReadsPdfAndCachedPreviewsStraightFromStorageWithoutConverting()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new CountingPreviewConverter();
        var pdf = await fixture.UploadStoredFileAsync("report.pdf", "%PDF-1.7 content"u8.ToArray());
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        await fixture.CreateFileHandler(converter).PreviewAsync(
            fixture.Seed.TeamId!.Value, 1, docx.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        var before = converter.ConversionCount;
        var files = fixture.CreateFileHandler(converter);
        var teamId = fixture.Seed.TeamId!.Value;

        var pdfSource = await files.GetPreviewSourceAsync(teamId, 1, pdf.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);
        var docxSource = await files.GetPreviewSourceAsync(teamId, 1, docx.Id, false, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        pdfSource.Value.Status.Should().Be("Ready");
        pdfSource.Value.Url.Should().Contain(fixture.StoredKeyOf(pdf.Id)).And.Contain("inline=1");
        pdfSource.Value.ExpiresAt.Should().Be(fixture.Clock.UtcNow.Add(SubmissionFileLimits.PresignedPreviewLifetime));
        docxSource.Value.Status.Should().Be("Ready");
        docxSource.Value.Url.Should().Contain($"{fixture.StoredKeyOf(docx.Id)}.preview.pdf");
        converter.ConversionCount.Should().Be(before);
    }

    [Fact]
    public async Task PreviewSource_ForANewDocxReportsPreparingAndNeverConvertsInTheRequest()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var converter = new CountingPreviewConverter();
        var runner = CreatePreviewRunner(fixture, converter);
        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var before = converter.ConversionCount;
        var files = fixture.CreateFileHandler(converter);
        var teamId = fixture.Seed.TeamId!.Value;

        var preparing = await files.GetPreviewSourceAsync(teamId, 1, docx.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);

        preparing.Value.Status.Should().Be("Preparing");
        preparing.Value.Url.Should().BeNull();
        preparing.Value.RetryAfterSeconds.Should().Be(1);
        converter.ConversionCount.Should().Be(before);

        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);
        var ready = await files.GetPreviewSourceAsync(teamId, 1, docx.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);
        ready.Value.Status.Should().Be("Ready");
        converter.ConversionCount.Should().Be(before + 1);
    }

    [Fact]
    public async Task PreviewSource_QueuesFilesThatWereNeverQueuedAndLetsFailedOnesBeRetried()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var files = fixture.CreateFileHandler();
        var teamId = fixture.Seed.TeamId!.Value;
        // A file uploaded before background generation existed: status None, no cache.
        await fixture.Context.SubmissionFiles.Where(item => item.Id == docx.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.PreviewStatus, SubmissionPreviewStatus.None));
        fixture.Context.ChangeTracker.Clear(); // a real request starts with a fresh context

        var queued = await files.GetPreviewSourceAsync(teamId, 1, docx.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);
        queued.Value.Status.Should().Be("Preparing");
        var row = await LoadFileAsync(fixture, docx.Id);
        row.PreviewStatus.Should().Be(SubmissionPreviewStatus.Pending);
        row.PreviewNextAttemptAtUtc.Should().BeCloseTo(fixture.Clock.UtcNow, TimeSpan.FromMilliseconds(1));

        await fixture.Context.SubmissionFiles.Where(item => item.Id == docx.Id).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.PreviewStatus, SubmissionPreviewStatus.Failed)
            .SetProperty(item => item.PreviewAttemptCount, 3)
            .SetProperty(item => item.PreviewLastError, "WORKSPACE_FILE_PREVIEW_UNAVAILABLE: boom"));
        fixture.Context.ChangeTracker.Clear();

        var failed = await files.GetPreviewSourceAsync(teamId, 1, docx.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);
        failed.Value.Status.Should().Be("Failed");
        failed.Value.Message.Should().Contain("download the original");
        failed.Value.Message.Should().NotContain("boom", "internal errors are never shown to users");

        var retried = await files.GetPreviewSourceAsync(teamId, 1, docx.Id, retry: true, fixture.Seed.ProposerUserId, SystemRoles.Student);
        retried.Value.Status.Should().Be("Preparing");
        var requeued = await LoadFileAsync(fixture, docx.Id);
        requeued.PreviewStatus.Should().Be(SubmissionPreviewStatus.Pending);
        requeued.PreviewAttemptCount.Should().Be(0);
        requeued.PreviewLastError.Should().BeNull();
    }

    [Fact]
    public async Task PreviewSource_ReportsTooLargeAndKeepsCloudinaryFilesOnTheServerSidePreview()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var huge = await fixture.UploadStoredFileAsync("huge.docx", BuildOfficeZip("word/document.xml"),
            reportedSize: SubmissionFileLimits.MaxPreviewConvertSizeBytes + 1);
        var legacy = await fixture.AddLegacyFileAsync(huge.Id);
        var files = fixture.CreateFileHandler();
        var teamId = fixture.Seed.TeamId!.Value;

        var tooLarge = await files.GetPreviewSourceAsync(teamId, 1, huge.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);
        var proxy = await files.GetPreviewSourceAsync(teamId, 1, legacy.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student);

        tooLarge.Value.Status.Should().Be("TooLarge");
        tooLarge.Value.Message.Should().Contain("30 MB");
        proxy.Value.Status.Should().Be("Proxy");
        proxy.Value.Url.Should().BeNull();
    }

    [Fact]
    public async Task PreviewSource_AppliesTheSameAccessRulesAsPreviewAndDownload()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var pdf = await fixture.UploadStoredFileAsync("report.pdf", "%PDF-1.7 content"u8.ToArray());
        var files = fixture.CreateFileHandler();
        var teamId = fixture.Seed.TeamId!.Value;
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");

        (await files.GetPreviewSourceAsync(teamId, 1, pdf.Id, false, outsider.Id, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await files.GetPreviewSourceAsync(fixture.Seed.OtherTeamId!.Value, 1, pdf.Id, false, fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await files.GetPreviewSourceAsync(teamId, 1, Guid.NewGuid(), false, fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.CommonNotFoundError);
        // Assigned mentor and class lecturer may read; the signed URL is only created after these checks.
        (await files.GetPreviewSourceAsync(teamId, 1, pdf.Id, false, fixture.Seed.MentorUserId, SystemRoles.Mentor)).IsSuccess.Should().BeTrue();
        (await files.GetPreviewSourceAsync(teamId, 1, pdf.Id, false, fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewSourceEndpoint_RequiresAuthenticationAndReturnsNoStoreNosniffResponses()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var runner = CreatePreviewRunner(fixture, new CountingPreviewConverter());
        await DrainPreviewQueueAsync(runner, fixture.Clock.UtcNow);
        var pdf = await fixture.UploadStoredFileAsync("report.pdf", "%PDF-1.7 content"u8.ToArray());
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var tokenService = fixture.Scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var student = await fixture.Context.Users.SingleAsync(item => item.Id == fixture.Seed.ProposerUserId);
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");
        using var client = _factory.CreateClient();

        (await client.GetAsync(PreviewSourceUrl(fixture, pdf.Id))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var forbidden = new HttpRequestMessage(HttpMethod.Get, PreviewSourceUrl(fixture, pdf.Id));
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(outsider, [SystemRoles.Student]).Token);
        (await client.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var bearer = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(student, [SystemRoles.Student]).Token);
        using var ready = new HttpRequestMessage(HttpMethod.Get, PreviewSourceUrl(fixture, pdf.Id)) { Headers = { Authorization = bearer } };
        using var readyResponse = await client.SendAsync(ready);
        readyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        readyResponse.Headers.CacheControl!.NoStore.Should().BeTrue();
        readyResponse.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
        using var json = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("Ready");
        var url = json.RootElement.GetProperty("data").GetProperty("url").GetString()!;
        url.Should().StartWith("https://testing-account-id.r2.cloudflarestorage.com/testing-bucket/submissions/");
        url.Should().Contain("X-Amz-Expires=900").And.Contain("response-content-disposition=inline").And.Contain("X-Amz-Signature=");
        url.Should().NotContain("testing-secret-access-key");

        using var preparing = new HttpRequestMessage(HttpMethod.Get, PreviewSourceUrl(fixture, docx.Id)) { Headers = { Authorization = bearer } };
        using var preparingResponse = await client.SendAsync(preparing);
        preparingResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        using var preparingJson = JsonDocument.Parse(await preparingResponse.Content.ReadAsStringAsync());
        preparingJson.RootElement.GetProperty("data").GetProperty("status").GetString().Should().Be("Preparing");
    }
}

using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Application.Features.Workspaces.GetCheckpointOverview;
using EHub.Contracts.Workspaces;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace EHub.IntegrationTests.Classes;

// Download / preview / delete for files stored on R2, next to files that still live on Cloudinary.
public sealed partial class TeamWorkflowIntegrationTests
{
    private const long TenMegabytes = 10L * 1024 * 1024;

    [Fact]
    public async Task R2Download_ProxiesSmallFilesAndEnforcesWorkspaceAccess()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var file = await fixture.UploadStoredFileAsync("report.pdf", "%PDF-r2-bytes"u8.ToArray());
        var files = fixture.CreateFileHandler();
        var teamId = fixture.Seed.TeamId!.Value;
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");

        var student = await files.DownloadAsync(teamId, 1, file.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        var lecturer = await files.DownloadAsync(teamId, 1, file.Id, fixture.Seed.LecturerId, SystemRoles.Lecturer);

        student.IsSuccess.Should().BeTrue();
        student.Value.Content.Should().Equal("%PDF-r2-bytes"u8.ToArray());
        student.Value.ContentType.Should().Be("application/pdf");
        student.Value.OriginalName.Should().Be("report.pdf");
        lecturer.IsSuccess.Should().BeTrue();
        (await files.DownloadAsync(teamId, 1, file.Id, outsider.Id, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        // The seeded mentor is assigned to this team, so read access is allowed; a mentor of another team is not.
        (await files.DownloadAsync(teamId, 1, file.Id, fixture.Seed.MentorUserId, SystemRoles.Mentor)).IsSuccess.Should().BeTrue();
        (await files.DownloadAsync(fixture.Seed.OtherTeamId!.Value, 1, file.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task R2DownloadUrl_IsOfferedOnlyForLargeR2FilesAndChecksAccessFirst()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var small = await fixture.UploadStoredFileAsync("small.pdf", "%PDF-small"u8.ToArray());
        var large = await fixture.UploadStoredFileAsync("large.pdf", "%PDF-large"u8.ToArray(), reportedSize: TenMegabytes + 1);
        var legacy = await fixture.AddLegacyFileAsync(large.Id);
        var teamId = fixture.Seed.TeamId!.Value;
        var files = fixture.CreateFileHandler();
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");

        var overview = await fixture.Scope.ServiceProvider.GetRequiredService<IGetWorkspaceCheckpointOverviewQueryHandler>()
            .HandleAsync(teamId, fixture.Seed.ProposerUserId, SystemRoles.Student);
        var listed = overview.Value.Submissions.Single(item => item.CheckpointNumber == 1).Files.ToDictionary(item => item.Id);
        listed[small.Id].CanDirectDownload.Should().BeFalse();
        listed[large.Id].CanDirectDownload.Should().BeTrue();
        listed[legacy.Id].CanDirectDownload.Should().BeFalse();

        var url = await files.GetDownloadUrlAsync(teamId, 1, large.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        url.IsSuccess.Should().BeTrue();
        url.Value.Url.Should().Contain(fixture.StoredKeyOf(large.Id)).And.Contain("expires=300").And.Contain("name=large.pdf");
        url.Value.ExpiresAt.Should().Be(fixture.Clock.UtcNow.Add(SubmissionFileLimits.PresignedDownloadLifetime));
        (await files.GetDownloadUrlAsync(teamId, 1, large.Id, fixture.Seed.LecturerId, SystemRoles.Lecturer)).IsSuccess.Should().BeTrue();

        (await files.GetDownloadUrlAsync(teamId, 1, large.Id, outsider.Id, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        (await files.GetDownloadUrlAsync(fixture.Seed.OtherTeamId!.Value, 1, large.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        // Cloudinary files never get a presigned URL; they keep the proxy download.
        (await files.GetDownloadUrlAsync(teamId, 1, legacy.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceValidationError);
        var legacyDownload = await files.DownloadAsync(teamId, 1, legacy.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        legacyDownload.IsSuccess.Should().BeTrue();
        legacyDownload.Value.Content.Should().Equal("%PDF-test"u8.ToArray());
    }

    [Fact]
    public async Task R2DownloadUrlEndpoint_RequiresAuthenticationAndReturnsSignedUrlWithoutSecrets()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var large = await fixture.UploadStoredFileAsync("Báo cáo.pdf", "%PDF-large"u8.ToArray(), reportedSize: TenMegabytes + 1);
        var tokenService = fixture.Scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var student = await fixture.Context.Users.SingleAsync(item => item.Id == fixture.Seed.ProposerUserId);
        var outsider = await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider");
        using var client = _factory.CreateClient();
        var url = $"/api/workspace/checkpoints/teams/{fixture.Seed.TeamId}/checkpoints/1/files/{large.Id}/download-url";

        (await client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var forbidden = new HttpRequestMessage(HttpMethod.Get, url);
        forbidden.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(outsider, [SystemRoles.Student]).Token);
        (await client.SendAsync(forbidden)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var allowed = new HttpRequestMessage(HttpMethod.Get, url);
        allowed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(student, [SystemRoles.Student]).Token);
        using var response = await client.SendAsync(allowed);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(payload);
        var signed = json.RootElement.GetProperty("data").GetProperty("url").GetString()!;
        signed.Should().StartWith("https://testing-account-id.r2.cloudflarestorage.com/testing-bucket/submissions/");
        signed.Should().Contain("X-Amz-Signature=").And.Contain("X-Amz-Expires=300").And.Contain("response-content-disposition=attachment");
        payload.Should().NotContain("testing-secret-access-key");
    }

    [Fact]
    public async Task R2Preview_ServesPdfAndConvertsAndCachesOfficeFilesOnR2()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var pdf = await fixture.UploadStoredFileAsync("report.pdf", "%PDF-original"u8.ToArray());
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var teamId = fixture.Seed.TeamId!.Value;
        var converter = new CountingPreviewConverter();
        var files = fixture.CreateFileHandler(converter);

        var pdfPreview = await files.PreviewAsync(teamId, 1, pdf.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        pdfPreview.IsSuccess.Should().BeTrue();
        pdfPreview.Value.Content.Should().Equal("%PDF-original"u8.ToArray());

        var first = await files.PreviewAsync(teamId, 1, docx.Id, fixture.Seed.LecturerId, SystemRoles.Lecturer);
        var second = await files.PreviewAsync(teamId, 1, docx.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);

        first.IsSuccess.Should().BeTrue();
        first.Value.FromCache.Should().BeFalse();
        second.IsSuccess.Should().BeTrue();
        second.Value.FromCache.Should().BeTrue();
        converter.ConversionCount.Should().Be(1);
        var stored = await fixture.Context.SubmissionFiles.AsNoTracking().SingleAsync(item => item.Id == docx.Id);
        stored.PreviewPdfPublicId.Should().Be($"{stored.StorageKey}.preview.pdf");
        stored.PreviewPdfUrl.Should().BeNull();
        stored.PreviewSourceVersionNumber.Should().Be(stored.VersionNumber);
        fixture.Storage.Uploaded.Should().ContainSingle().Which.Should().Be(stored.PreviewPdfPublicId);
        (await files.PreviewAsync(teamId, 1, docx.Id, (await CreateUserAsync(fixture.Context, SystemRoles.Student, "outsider")).Id, SystemRoles.Student))
            .Error.Code.Should().Be(ErrorCodes.WorkspaceAccessDenied);
    }

    [Fact]
    public async Task R2Preview_RejectsOversizedAndStructurallyInvalidOfficeFilesWithoutConverting()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var tooLarge = await fixture.UploadStoredFileAsync("huge.docx", BuildOfficeZip("word/document.xml"),
            reportedSize: SubmissionFileLimits.MaxPreviewConvertSizeBytes + 1);
        var notAnOfficeFile = await fixture.UploadStoredFileAsync("fake.pptx", BuildOfficeZip("unrelated.txt"));
        var converter = new CountingPreviewConverter();
        var files = fixture.CreateFileHandler(converter);
        var teamId = fixture.Seed.TeamId!.Value;

        var large = await files.PreviewAsync(teamId, 1, tooLarge.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);
        var fake = await files.PreviewAsync(teamId, 1, notAnOfficeFile.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);

        large.Error.Code.Should().Be(ErrorCodes.WorkspaceFilePreviewUnsupported);
        large.Error.Message.Should().Contain("30 MB");
        fake.Error.Code.Should().Be(ErrorCodes.WorkspaceFilePreviewConversionFailed);
        converter.ConversionCount.Should().Be(0);
    }

    [Fact]
    public async Task R2Delete_RemovesObjectAndCachedPreviewForTheUploaderOnly()
    {
        using var fixture = await CreateUploadFixtureAsync();
        var docx = await fixture.UploadStoredFileAsync("plan.docx", BuildOfficeZip("word/document.xml"));
        var teamId = fixture.Seed.TeamId!.Value;
        var files = fixture.CreateFileHandler(new CountingPreviewConverter());
        (await files.PreviewAsync(teamId, 1, docx.Id, fixture.Seed.ProposerUserId, SystemRoles.Student)).IsSuccess.Should().BeTrue();
        var key = fixture.StoredKeyOf(docx.Id);
        var previewKey = $"{key}.preview.pdf";
        fixture.Storage.Contains(previewKey).Should().BeTrue();

        (await files.DeleteAsync(teamId, 1, docx.Id, fixture.Seed.LecturerId, SystemRoles.Lecturer)).Error.Code
            .Should().Be(ErrorCodes.WorkspaceAccessDenied);
        fixture.Storage.Contains(key).Should().BeTrue();

        var deleted = await files.DeleteAsync(teamId, 1, docx.Id, fixture.Seed.ProposerUserId, SystemRoles.Student);

        deleted.IsSuccess.Should().BeTrue();
        fixture.Storage.Contains(key).Should().BeFalse();
        fixture.Storage.Contains(previewKey).Should().BeFalse();
        (await fixture.Context.SubmissionFiles.AnyAsync(item => item.Id == docx.Id)).Should().BeFalse();
        (await fixture.Context.SubmissionFiles.IgnoreQueryFilters().SingleAsync(item => item.Id == docx.Id)).IsDeleted.Should().BeTrue();
    }

    private static byte[] BuildOfficeZip(string entryName)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write("<xml/>");
        }

        return buffer.ToArray();
    }
}

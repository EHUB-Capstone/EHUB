using EHub.Api.Controllers;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Workspaces.CheckpointFiles;
using EHub.Contracts.Workspaces;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EHub.IntegrationTests.Classes;

public sealed class WorkspaceCheckpointPreviewControllerTests
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid teamId = Guid.NewGuid();
    private readonly Guid fileId = Guid.NewGuid();

    [Fact]
    public async Task PreviewFile_MapsAccessDeniedTo403AndMissingFileTo404()
    {
        var controller = CreateController();
        var forbidden = await controller.PreviewFile(teamId, 1, fileId,
            new PreviewResultHandler(Result.Failure<CheckpointFilePreview>(
                ErrorCodes.WorkspaceAccessDenied, "Access denied.")), CancellationToken.None);
        forbidden.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var missing = await controller.PreviewFile(teamId, 1, fileId,
            new PreviewResultHandler(Result.Failure<CheckpointFilePreview>(
                ErrorCodes.CommonNotFoundError, "Submitted file was not found.")), CancellationToken.None);
        missing.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task PreviewFile_ReturnsInlinePdfAndMapsUnsupportedFormat()
    {
        var controller = CreateController();
        var success = await controller.PreviewFile(teamId, 1, fileId,
            new PreviewResultHandler(Result.Success(new CheckpointFilePreview(
                "%PDF-preview"u8.ToArray(), "preview.pdf", FromCache: false))), CancellationToken.None);
        var pdf = success.Should().BeOfType<FileContentResult>().Subject;
        pdf.ContentType.Should().Be("application/pdf");
        pdf.FileDownloadName.Should().BeNullOrEmpty();

        var unsupported = await controller.PreviewFile(teamId, 1, fileId,
            new PreviewResultHandler(Result.Failure<CheckpointFilePreview>(
                ErrorCodes.WorkspaceFilePreviewUnsupported, "Unsupported.")), CancellationToken.None);
        unsupported.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status415UnsupportedMediaType);
    }

    [Theory]
    [InlineData(ErrorCodes.WorkspaceFilePreviewConversionFailed, StatusCodes.Status422UnprocessableEntity)]
    [InlineData(ErrorCodes.WorkspaceFilePreviewUnavailable, StatusCodes.Status503ServiceUnavailable)]
    public async Task PreviewFile_MapsConversionErrors(string errorCode, int expectedStatusCode)
    {
        var controller = CreateController();

        var result = await controller.PreviewFile(teamId, 1, fileId,
            new PreviewResultHandler(Result.Failure<CheckpointFilePreview>(
                errorCode, "Preview could not be generated.")), CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(expectedStatusCode);
    }

    private WorkspaceCheckpointsController CreateController() => new(new PreviewCurrentUser(userId))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private sealed class PreviewCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "preview-test@example.test";
        public IReadOnlyCollection<string> Roles => [SystemRoles.Student];
        public bool IsAuthenticated => true;
    }

    private sealed class PreviewResultHandler(Result<CheckpointFilePreview> previewResult) : ICheckpointFileHandler
    {
        public Task<Result<CheckpointFilePreview>> PreviewAsync(Guid teamId, int checkpointNumber, Guid fileId,
            Guid userId, string role, CancellationToken cancellationToken = default) => Task.FromResult(previewResult);

        public Task<Result<CheckpointFileDownloadUrlResponse>> GetDownloadUrlAsync(Guid teamId, int checkpointNumber, Guid fileId,
            Guid userId, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result<CheckpointFileDownload>> DownloadAsync(Guid teamId, int checkpointNumber, Guid fileId,
            Guid userId, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Result> DeleteAsync(Guid teamId, int checkpointNumber, Guid fileId, Guid userId, string role,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

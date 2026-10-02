using EHub.Contracts.Mentors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.Admin.Mentors;

public interface IMentorAdminHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> GetTemplateAsync(CancellationToken cancellationToken = default);
    Task<Result<MentorImportPreviewResponse>> PreviewImportAsync(Guid semesterId, IFormFile file, CancellationToken cancellationToken = default);
    Task<Result<MentorImportCommitResponse>> CommitImportAsync(CommitMentorImportRequest request, CancellationToken cancellationToken = default);
    Task<Result<MentorAllocationPreviewResponse>> PreviewAllocationAsync(PreviewMentorAllocationRequest request, CancellationToken cancellationToken = default);
    Task<Result<MentorAllocationCommitResponse>> CommitAllocationAsync(CommitMentorAllocationRequest request, CancellationToken cancellationToken = default);
}

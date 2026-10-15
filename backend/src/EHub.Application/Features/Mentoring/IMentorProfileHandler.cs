using EHub.Contracts.Mentoring;
using EHub.Shared.Results;

namespace EHub.Application.Features.Mentoring;

public interface IMentorProfileHandler
{
    Task<Result<MentorProfileResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken);
    Task<Result<MentorProfileResponse>> UpdateMineAsync(Guid userId, UpdateMentorProfileRequest request, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<MentorProfileResponse>>> GetDirectoryAsync(Guid userId, string role, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<MentorRecommendationResponse>>> RecommendAsync(Guid teamId, Guid userId, string role, CancellationToken cancellationToken);
    Task<Result<MentorProfileResponse>> UploadDocumentAsync(Guid userId, string kind, Stream content,
        string fileName, long length, CancellationToken cancellationToken);
    Task<Result<(byte[] Content, string FileName, string ContentType)>> DownloadDocumentAsync(Guid mentorId,
        string kind, Guid userId, string role, CancellationToken cancellationToken);
}

using EHub.Contracts.Mentors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Admin.MentorProfiles;

public interface IMentorProfileHandler
{
    Task<Result<MentorProfileResponse>> GetAsync(Guid mentorProfileId, CancellationToken cancellationToken = default);
    Task<Result<MentorProfileResponse>> UpdateAsync(Guid mentorProfileId, UpdateMentorProfileRequest request, CancellationToken cancellationToken = default);
}

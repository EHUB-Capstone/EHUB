using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Mentoring;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Mentoring.UploadCv;

public interface IAdminMentorCvUploadHandler
{
    Task<Result<MentorProfileResponse>> UploadAsync(Guid mentorId, Stream content, string fileName,
        long length, bool replaceExisting, CancellationToken cancellationToken);
}

public sealed class AdminMentorCvUploadHandler(IApplicationDbContext context, ICurrentUserService currentUser,
    IMentorProfileHandler profiles) : IAdminMentorCvUploadHandler
{
    public async Task<Result<MentorProfileResponse>> UploadAsync(Guid mentorId, Stream content, string fileName,
        long length, bool replaceExisting, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null || currentUser.UserId == Guid.Empty)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonUnauthorizedError,
                "An authenticated administrator is required.");
        if (!currentUser.Roles.Contains(SystemRoles.Admin))
            return Result.Failure<MentorProfileResponse>(ErrorCodes.ClassAccessDenied,
                "Only an administrator can upload a mentor CV.");

        // Import drafts have no account-backed MentorProfile and cannot receive a CV.
        // Resolve the selected profile on the server; never accept an owner id from the form.
        var ownerId = await context.MentorProfiles.AsNoTracking()
            .Where(profile => profile.Id == mentorId && profile.UserId != Guid.Empty &&
                context.Users.Any(user => user.Id == profile.UserId))
            .Select(profile => (Guid?)profile.UserId).FirstOrDefaultAsync(cancellationToken);
        if (ownerId is null)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonNotFoundError,
                "An existing mentor profile with an account is required before uploading a CV.");

        return await profiles.UploadDocumentAsync(ownerId.Value, "cv", content, fileName,
            length, cancellationToken, replaceExisting);
    }
}

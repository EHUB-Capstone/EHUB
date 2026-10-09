using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Mentoring.ManageProfiles;

public interface IAdminMentorProfileHandler
{
    Task<Result<MentorProfileResponse>> SaveAsync(Guid? id, SaveAdminMentorProfileRequest request, CancellationToken ct);
}

public sealed class AdminMentorProfileHandler(IApplicationDbContext context, ICurrentUserService currentUser,
    IPasswordHasher passwordHasher) : IAdminMentorProfileHandler
{
    public async Task<Result<MentorProfileResponse>> SaveAsync(Guid? id, SaveAdminMentorProfileRequest request, CancellationToken ct)
    {
        if (!currentUser.Roles.Contains(SystemRoles.Admin))
            return Result.Failure<MentorProfileResponse>(ErrorCodes.ClassAccessDenied, "Only an administrator can manage mentor profiles.");
        var validation = await new SaveAdminMentorProfileRequestValidator().ValidateAsync(request, ct);
        if (!validation.IsValid) return Invalid(validation.Errors[0].ErrorMessage);
        if (id is null && string.IsNullOrWhiteSpace(request.TemporaryPassword))
            return Invalid("A temporary password is required for the new mentor account.");
        var profile = id is null ? null : await context.MentorProfiles.Include(x => x.User).Include(x => x.Experiences)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (id is not null && profile is null)
            return Result.Failure<MentorProfileResponse>(ErrorCodes.CommonNotFoundError, "Mentor profile was not found.");
        var email = request.Email.Trim().ToLowerInvariant();
        var userId = profile?.UserId ?? Guid.Empty;
        if (await context.Users.IgnoreQueryFilters().AnyAsync(x => x.NormalizedEmail == email && x.Id != userId, ct))
            return Invalid("Email already belongs to an account. Edit its existing mentor profile instead.");
        if (profile is not null && profile.MentorType != request.Profile.MentorType &&
            await context.MentorAssignments.AnyAsync(x => x.MentorProfileId == profile.Id &&
                x.Status == MentorAssignmentStatus.Active && x.EndedAt == null, ct))
            return Invalid("Reassign active teams before changing this mentor's type.");
        if (profile is null)
        {
            var role = await context.Roles.FirstOrDefaultAsync(x => x.Name == SystemRoles.Mentor, ct);
            if (role is null) return Invalid("Mentor role is not configured.");
            var user = new User { FullName = request.FullName.Trim(), Email = email, NormalizedEmail = email,
                PasswordHash = passwordHasher.Hash(request.TemporaryPassword!), CreatedBy = currentUser.UserId };
            user.UserRoles.Add(new UserRole { User = user, UserId = user.Id, Role = role, RoleId = role.Id, AssignedBy = currentUser.UserId });
            profile = new MentorProfile { User = user, UserId = user.Id, CreatedBy = currentUser.UserId };
            context.MentorProfiles.Add(profile);
        }
        profile.User.FullName = request.FullName.Trim();
        profile.User.Email = email;
        profile.User.NormalizedEmail = email;
        profile.User.UpdatedBy = currentUser.UserId;
        profile.MentorType = request.Profile.MentorType;
        MentorProfileHandler.ApplyMetadata(profile, request.Profile);
        profile.UpdatedBy = currentUser.UserId;
        try { await context.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            context.ClearChanges();
            return Invalid("The mentor could not be saved. Reload and check whether the email already exists.");
        }
        return Result.Success(MentorProfileHandler.Map(profile));
    }

    private static Result<MentorProfileResponse> Invalid(string message) =>
        Result.Failure<MentorProfileResponse>(ErrorCodes.CommonValidationError, message);
}

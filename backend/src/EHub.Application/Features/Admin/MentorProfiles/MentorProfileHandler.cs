using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Mentors;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.MentorProfiles;

public sealed class MentorProfileHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IValidator<UpdateMentorProfileRequest> validator) : IMentorProfileHandler
{
    public async Task<Result<MentorProfileResponse>> GetAsync(Guid mentorProfileId, CancellationToken cancellationToken = default)
    {
        var denied = RequireAdmin();
        if (denied is not null) return Result.Failure<MentorProfileResponse>(denied);

        var profile = await context.MentorProfiles.AsNoTracking().Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == mentorProfileId, cancellationToken);
        if (profile is null) return Result.Failure<MentorProfileResponse>(NotFound());
        return Result.Success(await ToResponseAsync(profile, cancellationToken));
    }

    public async Task<Result<MentorTagSuggestionsResponse>> GetTagSuggestionsAsync(CancellationToken cancellationToken = default)
    {
        var denied = RequireAdmin();
        if (denied is not null) return Result.Failure<MentorTagSuggestionsResponse>(denied);

        var rows = await context.MentorProfiles.AsNoTracking()
            .Select(item => new { item.Expertise, item.StartupDomains, item.TechnologySkills, item.MentorTags })
            .ToListAsync(cancellationToken);
        return Result.Success(new MentorTagSuggestionsResponse
        {
            Expertise = MostUsed(rows.SelectMany(item => item.Expertise ?? [])),
            StartupDomains = MostUsed(rows.SelectMany(item => item.StartupDomains ?? [])),
            TechnologySkills = MostUsed(rows.SelectMany(item => item.TechnologySkills ?? [])),
            MentorTags = MostUsed(rows.SelectMany(item => item.MentorTags ?? []))
        });
    }

    // Groups spellings that differ only by case, keeps the most common spelling and orders by how many mentors use it.
    private static string[] MostUsed(IEnumerable<string> tags) => tags
        .Where(tag => !string.IsNullOrWhiteSpace(tag))
        .GroupBy(tag => tag, StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
        .Select(group => group.GroupBy(tag => tag).OrderByDescending(spelling => spelling.Count()).First().Key)
        .Take(200)
        .ToArray();

    public async Task<Result<MentorProfileResponse>> UpdateAsync(Guid mentorProfileId, UpdateMentorProfileRequest request, CancellationToken cancellationToken = default)
    {
        var denied = RequireAdmin();
        if (denied is not null) return Result.Failure<MentorProfileResponse>(denied);

        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Result.Failure<MentorProfileResponse>(new Error(ErrorCodes.CommonValidationError, validation.Errors[0].ErrorMessage));

        var profile = await context.MentorProfiles.Include(item => item.User)
            .FirstOrDefaultAsync(item => item.Id == mentorProfileId, cancellationToken);
        if (profile is null) return Result.Failure<MentorProfileResponse>(NotFound());
        if (!uint.TryParse(request.RowVersion, out var expectedVersion) || profile.Version != expectedVersion)
            return Result.Failure<MentorProfileResponse>(Changed());

        profile.Status = Enum.Parse<MentorProfileStatus>(request.Status, ignoreCase: true);
        profile.Expertise = MentorProfileRules.NormalizeExpertise(request.Expertise).Values;
        profile.StartupDomains = MentorProfileRules.NormalizeTags("Startup domain", request.StartupDomains).Values;
        profile.TechnologySkills = MentorProfileRules.NormalizeTags("Technology skill", request.TechnologySkills).Values;
        profile.MentorTags = MentorProfileRules.NormalizeTags("Mentor tag", request.MentorTags).Values;
        profile.Bio = MentorProfileRules.Clean(request.Bio);
        profile.AvailabilityNote = MentorProfileRules.Clean(request.AvailabilityNote);
        profile.Organization = MentorProfileRules.Clean(request.Organization);
        profile.Department = MentorProfileRules.Clean(request.Department);
        profile.JobTitle = MentorProfileRules.Clean(request.JobTitle);
        profile.ContractType = MentorProfileRules.Clean(request.ContractType);
        profile.EducationLevel = MentorProfileRules.Clean(request.EducationLevel);
        profile.CurrentAddress = MentorProfileRules.Clean(request.CurrentAddress);
        profile.LinkedInUrl = MentorProfileRules.Clean(request.LinkedInUrl);
        profile.FptEmail = MentorProfileRules.Clean(request.FptEmail)?.ToLowerInvariant();
        profile.DateOfBirth = request.DateOfBirth;
        profile.UpdatedBy = currentUser.UserId;
        profile.UpdatedAt = DateTime.UtcNow;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<MentorProfileResponse>(Changed());
        }
        return Result.Success(await ToResponseAsync(profile, cancellationToken));
    }

    private async Task<MentorProfileResponse> ToResponseAsync(MentorProfile profile, CancellationToken cancellationToken)
    {
        var activeTeams = await context.MentorAssignments.AsNoTracking().CountAsync(item =>
            item.MentorProfileId == profile.Id && item.Status == MentorAssignmentStatus.Active && item.EndedAt == null &&
            item.Team.Status == TeamStatus.Active, cancellationToken);
        return new MentorProfileResponse
        {
            Id = profile.Id,
            UserId = profile.UserId,
            FullName = profile.User.FullName,
            Email = profile.User.Email,
            Phone = profile.User.Phone,
            AvatarUrl = profile.User.AvatarUrl,
            MentorType = profile.Type.ToString(),
            Status = profile.Status.ToString(),
            Expertise = profile.Expertise ?? [],
            StartupDomains = profile.StartupDomains ?? [],
            TechnologySkills = profile.TechnologySkills ?? [],
            MentorTags = profile.MentorTags ?? [],
            Bio = profile.Bio,
            AvailabilityNote = profile.AvailabilityNote,
            Organization = profile.Organization,
            Department = profile.Department,
            JobTitle = profile.JobTitle,
            ContractType = profile.ContractType,
            EducationLevel = profile.EducationLevel,
            CurrentAddress = profile.CurrentAddress,
            LinkedInUrl = profile.LinkedInUrl,
            FptEmail = profile.FptEmail,
            DateOfBirth = profile.DateOfBirth,
            ActiveTeamCount = activeTeams,
            RowVersion = profile.Version.ToString()
        };
    }

    private Error? RequireAdmin() =>
        currentUser.UserId is null || !currentUser.Roles.Contains(SystemRoles.Admin, StringComparer.OrdinalIgnoreCase)
            ? new Error(ErrorCodes.CommonForbiddenError, "Only an administrator can manage mentor profiles.")
            : null;

    private static Error NotFound() => new(ErrorCodes.MentorProfileNotFound, "The mentor profile was not found.");
    private static Error Changed() => new(ErrorCodes.MentorProfileConflict, "The mentor profile was changed by someone else. Reload it and try again.");
}

using System.Text.Json;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Auth.UpdateOwnMajor;

internal sealed record PreparedMajorUpdate(
    string MajorCode,
    IReadOnlyCollection<Guid> UpdatedClassIds,
    bool HasChanges);

internal static class StudentMajorUpdatePolicy
{
    private static readonly HashSet<string> GroupOneMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        MajorCodes.BBA_HM,
        MajorCodes.BBA_FIN,
        MajorCodes.BBA_IB,
        MajorCodes.BBA_MC,
        MajorCodes.BBA_MKT,
        MajorCodes.BEN,
        MajorCodes.BBA_TM
    };

    private static readonly HashSet<string> GroupTwoMajorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        MajorCodes.BIT_AI,
        MajorCodes.BIT_GD,
        MajorCodes.BIT_IA,
        MajorCodes.BIT_SE
    };

    public static async Task<Result<PreparedMajorUpdate>> PrepareAsync(
        IApplicationDbContext context,
        Student student,
        Guid actorUserId,
        string majorCode,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var normalizedMajorCode = majorCode.Trim().ToUpperInvariant();
        if (!MajorCodes.IsValid(normalizedMajorCode))
        {
            return Result.Failure<PreparedMajorUpdate>(
                ErrorCodes.AuthInvalidMajor,
                "Select a valid student major.");
        }

        var activeEnrollments = await context.ClassStudents
            .Include(enrollment => enrollment.Class)
            .Where(enrollment =>
                enrollment.StudentId == student.Id &&
                enrollment.EnrollmentStatus == EnrollmentStatus.Active &&
                (enrollment.Class.Status == ClassStatus.Draft ||
                 enrollment.Class.Status == ClassStatus.Active ||
                 enrollment.Class.Status == ClassStatus.Inactive))
            .ToListAsync(cancellationToken);

        var profileChanged = !string.Equals(
            student.MajorCode?.Trim(),
            normalizedMajorCode,
            StringComparison.OrdinalIgnoreCase);
        var enrollmentsToUpdate = activeEnrollments
            .Where(enrollment => !string.Equals(
                enrollment.MajorCodeAtEnrollment?.Trim(),
                normalizedMajorCode,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if ((profileChanged || enrollmentsToUpdate.Length > 0) &&
            activeEnrollments.Any(enrollment => enrollment.Class.IsEnrollmentMajorLocked))
        {
            var lockedClasses = activeEnrollments
                .Where(enrollment => enrollment.Class.IsEnrollmentMajorLocked)
                .Select(enrollment => enrollment.Class.ClassCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase);
            return Result.Failure<PreparedMajorUpdate>(
                ErrorCodes.ClassEnrollmentMajorLocked,
                $"Your major is locked in {string.Join(", ", lockedClasses)}. Contact the assigned lecturer before changing it.");
        }

        var newGroup = GetMajorGroup(normalizedMajorCode);
        var changesComposition = activeEnrollments.Any(enrollment =>
            GetMajorGroup(StudentEnrollmentRules.ResolveEffectiveMajorCode(
                enrollment.MajorCodeAtEnrollment,
                student.MajorCode)) != newGroup);

        if (changesComposition)
        {
            var teamError = await ValidateActiveTeamsAsync(
                context,
                student.Id,
                normalizedMajorCode,
                cancellationToken);
            if (teamError != null)
            {
                return Result.Failure<PreparedMajorUpdate>(teamError);
            }

            var proposalError = await ValidateOpenProposalsAsync(
                context,
                student.Id,
                normalizedMajorCode,
                cancellationToken);
            if (proposalError != null)
            {
                return Result.Failure<PreparedMajorUpdate>(proposalError);
            }
        }

        if (profileChanged)
        {
            student.MajorCode = normalizedMajorCode;
            student.UpdatedAt = nowUtc;
            student.UpdatedBy = actorUserId;
        }

        foreach (var enrollment in enrollmentsToUpdate)
        {
            var previousMajorCode = enrollment.MajorCodeAtEnrollment;
            enrollment.MajorCodeAtEnrollment = normalizedMajorCode;
            enrollment.MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified;
            enrollment.MajorVerifiedAtUtc = null;
            enrollment.MajorVerifiedByUserId = null;
            enrollment.UpdatedAt = nowUtc;

            context.ClassAuditLogs.Add(new ClassAuditLog
            {
                ClassId = enrollment.ClassId,
                Action = "STUDENT_MAJOR_UPDATED",
                PerformedByUserId = actorUserId,
                OccurredAtUtc = nowUtc,
                DetailsJson = JsonSerializer.Serialize(new
                {
                    StudentId = student.Id,
                    PreviousMajorCode = previousMajorCode,
                    NewMajorCode = normalizedMajorCode,
                    Source = "StudentSelfService"
                })
            });
            ClassOutbox.Enqueue(context, "Class.MajorUpdated.v1", enrollment.ClassId, new
            {
                StudentId = student.Id,
                MajorCode = normalizedMajorCode
            }, nowUtc);
        }

        var updatedClassIds = enrollmentsToUpdate
            .Select(enrollment => enrollment.ClassId)
            .Distinct()
            .ToArray();
        return Result.Success(new PreparedMajorUpdate(
            normalizedMajorCode,
            updatedClassIds,
            profileChanged || updatedClassIds.Length > 0));
    }

    private static async Task<Error?> ValidateActiveTeamsAsync(
        IApplicationDbContext context,
        Guid studentId,
        string proposedMajorCode,
        CancellationToken cancellationToken)
    {
        var teams = await context.Teams
            .AsNoTracking()
            .Where(team =>
                team.Status == TeamStatus.Active &&
                team.TeamMembers.Any(member =>
                    member.StudentId == studentId &&
                    member.CountsTowardActiveTeam))
            .Include(team => team.TeamMembers)
                .ThenInclude(member => member.ClassStudent)
                .ThenInclude(enrollment => enrollment.Student)
            .ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            var majors = team.TeamMembers
                .Where(member => member.CountsTowardActiveTeam)
                .Select(member => member.StudentId == studentId
                    ? proposedMajorCode
                    : StudentEnrollmentRules.ResolveEffectiveMajorCode(
                        member.ClassStudent.MajorCodeAtEnrollment,
                        member.ClassStudent.Student.MajorCode))
                .ToArray();
            if (HasValidComposition(majors)) continue;

            return new Error(
                ErrorCodes.TeamMajorCompositionInvalid,
                $"Changing your major would make team '{team.TeamName}' invalid. A team must keep at least one BBA/BEN member and one BIT member.");
        }

        return null;
    }

    private static async Task<Error?> ValidateOpenProposalsAsync(
        IApplicationDbContext context,
        Guid studentId,
        string proposedMajorCode,
        CancellationToken cancellationToken)
    {
        var proposals = await context.TeamProposals
            .AsNoTracking()
            .Where(proposal =>
                (proposal.Status == TeamProposalStatus.Draft ||
                 proposal.Status == TeamProposalStatus.Pending ||
                 proposal.Status == TeamProposalStatus.NeedsRevision) &&
                proposal.Members.Any(member =>
                    member.StudentId == studentId &&
                    member.IsIncluded &&
                    member.CountsTowardOpenProposal))
            .Include(proposal => proposal.Members)
                .ThenInclude(member => member.ClassStudent)
                .ThenInclude(enrollment => enrollment.Student)
            .ToListAsync(cancellationToken);

        foreach (var proposal in proposals)
        {
            var majors = proposal.Members
                .Where(member => member.IsIncluded && member.CountsTowardOpenProposal)
                .Select(member => member.StudentId == studentId
                    ? proposedMajorCode
                    : StudentEnrollmentRules.ResolveEffectiveMajorCode(
                        member.ClassStudent.MajorCodeAtEnrollment,
                        member.ClassStudent.Student.MajorCode))
                .ToArray();
            if (HasValidComposition(majors)) continue;

            return new Error(
                ErrorCodes.TeamMajorCompositionInvalid,
                $"Changing your major would make the open proposal '{proposal.TeamName}' invalid. Update the proposal membership first.");
        }

        return null;
    }

    private static bool HasValidComposition(IEnumerable<string?> majorCodes)
    {
        var groups = majorCodes.Select(GetMajorGroup).ToArray();
        return groups.All(group => group != 0) && groups.Contains(1) && groups.Contains(2);
    }

    private static int GetMajorGroup(string? majorCode)
    {
        if (string.IsNullOrWhiteSpace(majorCode)) return 0;
        var normalized = majorCode.Trim();
        if (GroupOneMajorCodes.Contains(normalized)) return 1;
        return GroupTwoMajorCodes.Contains(normalized) ? 2 : 0;
    }
}

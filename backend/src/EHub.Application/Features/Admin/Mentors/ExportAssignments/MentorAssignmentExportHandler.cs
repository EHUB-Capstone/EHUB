using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Classes.ExportAdminClassData;
using EHub.Application.Features.Classes.ExportClassRoster;
using EHub.Application.Features.Teams.Common;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Mentors.ExportAssignments;

public interface IMentorAssignmentExportHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> ExportAsync(
        Guid semesterId, CancellationToken cancellationToken = default);
}

public sealed class MentorAssignmentExportHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IMentorAssignmentExportHandler
{
    public async Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> ExportAsync(
        Guid semesterId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is null ||
            !currentUser.Roles.Any(role => role.Equals(SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)))
            return Failure(ErrorCodes.CommonUnauthorizedError, "An authenticated administrator is required.");

        var semester = semesterId == Guid.Empty
            ? null
            : await context.Semesters.AsNoTracking().FirstOrDefaultAsync(item => item.Id == semesterId, cancellationToken);
        if (semester is null) return Failure(ErrorCodes.SemesterNotFound, "The selected semester was not found.");

        var subjectCodes = new[] { MentorAssignmentExportWorkbookBuilder.FirstSubjectCode, MentorAssignmentExportWorkbookBuilder.SecondSubjectCode };
        var classes = await context.Classes.AsNoTracking()
            .Include(item => item.Course)
            .Include(item => item.Semester)
            .Where(item => item.SemesterId == semesterId && subjectCodes.Contains(item.Course.Code))
            .ToListAsync(cancellationToken);

        var completedClassIds = classes.Where(UsesCompletedRoster).Select(item => item.Id).ToArray();
        var activeClassIds = classes.Where(item => !UsesCompletedRoster(item)).Select(item => item.Id).ToArray();
        var roster = await context.ClassStudents.AsNoTracking()
            .Include(enrollment => enrollment.Student)
            .Include(enrollment => enrollment.TeamMembers).ThenInclude(member => member.Team).ThenInclude(team => team.Project)
            .Where(enrollment =>
                (activeClassIds.Contains(enrollment.ClassId) && enrollment.EnrollmentStatus == EnrollmentStatus.Active) ||
                (completedClassIds.Contains(enrollment.ClassId) && enrollment.EnrollmentStatus == EnrollmentStatus.Completed))
            .ToListAsync(cancellationToken);

        var registeredMajorByEmail = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            context, roster.Select(enrollment => enrollment.Student.Email), cancellationToken);

        var inEffect = await LoadAssignmentsInEffectAsync(semesterId, cancellationToken);
        var mentorsByTeam = inEffect
            .GroupBy(item => item.TeamId)
            .ToDictionary(
                group => group.Key,
                group => new ClassRosterMentorNames(
                    group.FirstOrDefault(item => item.Slot == MentorType.Enterprise)?.MentorName ?? string.Empty,
                    group.FirstOrDefault(item => item.Slot == MentorType.Academic)?.MentorName ?? string.Empty));

        var rosterByClass = roster.GroupBy(item => item.ClassId).ToDictionary(group => group.Key, group => group.ToArray());
        ClassRosterExportSection[] SectionsFor(string subjectCode) => AdminClassExportOrdering
            .Apply(classes.Where(item => IsSubject(item.Course.Code, subjectCode)).ToList())
            .Select(item => new ClassRosterExportSection(item, rosterByClass.GetValueOrDefault(item.Id) ?? [], mentorsByTeam))
            .ToArray();
        var firstSections = SectionsFor(MentorAssignmentExportWorkbookBuilder.FirstSubjectCode);
        var secondSections = SectionsFor(MentorAssignmentExportWorkbookBuilder.SecondSubjectCode);

        // The summary counts exactly the teams that appear on the roster sheets, so both always agree.
        var teamsOnSheets = firstSections.Concat(secondSections)
            .SelectMany(section => section.Roster.SelectMany(enrollment => enrollment.TeamMembers
                .Where(member => member.CountsTowardActiveTeam && member.Team is { Status: TeamStatus.Active })
                .Select(member => (member.Team.Id, SubjectCode: section.Class.Course.Code))))
            .Distinct()
            .ToDictionary(item => item.Id, item => item.SubjectCode);
        var teamMentors = inEffect
            .Where(item => teamsOnSheets.ContainsKey(item.TeamId))
            .Select(item => new ExportTeamMentor(item.MentorProfileId, teamsOnSheets[item.TeamId]))
            .ToArray();

        var mentors = await LoadMentorsAsync(semesterId, teamMentors.Select(item => item.MentorId).ToHashSet(), cancellationToken);

        var bytes = MentorAssignmentExportWorkbookBuilder.Build(
            semester.Code, firstSections, secondSections, registeredMajorByEmail, mentors, teamMentors);
        return Result.Success((bytes, ClassRosterExportWorkbookBuilder.ContentType, $"{semester.Code}_mentor_assignments.xlsx"));
    }

    // The mentor of a slot is the latest assignment that is still active, or that ended together with its class being
    // completed. Assignments ended earlier (for example a mentor who was replaced) are not part of the result.
    private async Task<List<InEffectAssignment>> LoadAssignmentsInEffectAsync(Guid semesterId, CancellationToken cancellationToken)
    {
        var rows = await context.MentorAssignments.AsNoTracking()
            .Where(item => item.Team.Class.SemesterId == semesterId && item.Team.Status == TeamStatus.Active &&
                ((item.Status == MentorAssignmentStatus.Active && item.EndedAt == null) ||
                 (item.Status == MentorAssignmentStatus.Ended && item.EndedAt != null &&
                  item.Team.Class.CompletedAtUtc != null && item.EndedAt >= item.Team.Class.CompletedAtUtc)))
            .Select(item => new InEffectAssignment(
                item.TeamId, item.Slot, item.MentorProfileId, item.MentorProfile.User.FullName, item.AssignedAt, item.Id))
            .ToListAsync(cancellationToken);
        // Mentors without an account are exported by name, marked so that nobody mistakes them for registered mentors.
        rows.AddRange(await context.TemporaryMentorAssignments.AsNoTracking()
            .Where(item => item.Team.Class.SemesterId == semesterId && item.Team.Status == TeamStatus.Active &&
                ((item.Status == MentorAssignmentStatus.Active && item.EndedAt == null) ||
                 (item.Status == MentorAssignmentStatus.Ended && item.EndedAt != null &&
                  item.Team.Class.CompletedAtUtc != null && item.EndedAt >= item.Team.Class.CompletedAtUtc)))
            .Select(item => new InEffectAssignment(
                item.TeamId, item.Slot, item.DraftId, item.Draft.FullName + TemporaryMentors.ExportSuffix, item.AssignedAt, item.Id))
            .ToListAsync(cancellationToken));

        return rows
            .GroupBy(item => (item.TeamId, item.Slot))
            .Select(group => group.OrderByDescending(item => item.AssignedAt).ThenBy(item => item.AssignmentId).First())
            .ToList();
    }

    // Every mentor active in the semester is listed (even without teams), plus mentors that carry a team on the sheets.
    private async Task<List<ExportMentor>> LoadMentorsAsync(Guid semesterId, HashSet<Guid> mentorsWithTeams, CancellationToken cancellationToken)
    {
        var ids = mentorsWithTeams.ToArray();
        var registered = await context.MentorProfiles.AsNoTracking()
            .Where(profile => ids.Contains(profile.Id) ||
                context.SemesterStaffAssignments.Any(staff =>
                    staff.SemesterId == semesterId && staff.UserId == profile.UserId &&
                    staff.Role == SemesterStaffRole.Mentor && staff.Status == SemesterStaffStatus.Active))
            .Select(profile => new ExportMentor(profile.Id, profile.User.FullName, profile.Type, profile.ContractType))
            .ToListAsync(cancellationToken);
        var temporary = await context.MentorImportDrafts.AsNoTracking()
            .Where(draft => ids.Contains(draft.Id))
            .Select(draft => new ExportMentor(draft.Id, draft.FullName + TemporaryMentors.ExportSuffix, draft.Type, draft.ContractType))
            .ToListAsync(cancellationToken);
        return registered.Concat(temporary).ToList();
    }

    private static bool UsesCompletedRoster(Class item) =>
        item.Status == ClassStatus.Completed ||
        item.Status == ClassStatus.Archived && item.StatusBeforeArchive == ClassStatus.Completed;

    private static bool IsSubject(string code, string expected) => string.Equals(code, expected, StringComparison.OrdinalIgnoreCase);

    private static Result<(byte[], string, string)> Failure(string code, string message) =>
        Result.Failure<(byte[], string, string)>(new Error(code, message));

    private sealed record InEffectAssignment(
        Guid TeamId, MentorType Slot, Guid MentorProfileId, string MentorName, DateTime AssignedAt, Guid AssignmentId);
}

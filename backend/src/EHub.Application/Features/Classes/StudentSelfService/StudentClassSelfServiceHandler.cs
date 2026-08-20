using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Application.Features.Teams.Common;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.StudentSelfService;

public sealed class StudentClassSelfServiceHandler : IStudentClassSelfServiceHandler
{
    private readonly IApplicationDbContext _context;

    public StudentClassSelfServiceHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MyClassesResponse>> GetMyClassesAsync(
        Guid userId, string role, string scope = "Current", CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveStudentIdAsync(userId, role, cancellationToken);
        if (studentId.IsFailure) return Result.Failure<MyClassesResponse>(studentId.Error);
        var normalizedScope = scope.Trim();
        if (!string.Equals(normalizedScope, "Current", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedScope, "History", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<MyClassesResponse>(new Error(ErrorCodes.ClassValidationError, "Scope must be Current or History."));
        var isHistory = string.Equals(normalizedScope, "History", StringComparison.OrdinalIgnoreCase);

        var enrollmentQuery = _context.ClassStudents.AsNoTracking()
            .Include(item => item.Class).ThenInclude(item => item.Course)
            .Include(item => item.Class).ThenInclude(item => item.Semester)
            .Include(item => item.Class).ThenInclude(item => item.PrimaryLecturer)
            .Where(item => item.StudentId == studentId.Value);
        enrollmentQuery = isHistory
            ? enrollmentQuery.Where(item =>
                item.EnrollmentStatus == EnrollmentStatus.Completed &&
                (item.Class.Status == ClassStatus.Completed || item.Class.Status == ClassStatus.Archived))
            : enrollmentQuery.Where(item =>
                item.EnrollmentStatus == EnrollmentStatus.Active &&
                (item.Class.Status == ClassStatus.Draft || item.Class.Status == ClassStatus.Active));

        var enrollments = await enrollmentQuery
            .OrderByDescending(item => item.Class.Semester.Year)
            .ThenBy(item => item.Class.ClassCode)
            .ToListAsync(cancellationToken);
        var mentorsByClass = await LoadMentorsByClassAsync(enrollments.Select(item => item.ClassId).ToArray(), cancellationToken);
        var result = enrollments
            .Select(item => MapClass(
                item.Class,
                mentorsByClass.GetValueOrDefault(item.ClassId) ?? Array.Empty<MentorSummaryDto>(),
                item.EnrollmentStatus))
            .ToArray();
        return Result.Success(new MyClassesResponse { Classes = result });
    }

    public async Task<Result<StudentClassDetailResponse>> GetClassDetailByIdentifierAsync(
        string classIdentifier,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var classId = await ResolveClassIdAsync(classIdentifier, cancellationToken);
        if (classId.IsFailure)
        {
            return Result.Failure<StudentClassDetailResponse>(classId.Error);
        }

        return await GetClassDetailAsync(classId.Value, userId, role, cancellationToken);
    }

    public async Task<Result<StudentClassDetailResponse>> GetClassDetailAsync(
        Guid classId, Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveStudentIdAsync(userId, role, cancellationToken);
        if (studentId.IsFailure) return Result.Failure<StudentClassDetailResponse>(studentId.Error);
        var enrollmentStatus = await _context.ClassStudents.AsNoTracking()
            .Where(item => item.ClassId == classId && item.StudentId == studentId.Value &&
                (item.EnrollmentStatus == EnrollmentStatus.Active || item.EnrollmentStatus == EnrollmentStatus.Completed))
            .Select(item => (EnrollmentStatus?)item.EnrollmentStatus)
            .FirstOrDefaultAsync(cancellationToken);
        if (!enrollmentStatus.HasValue)
            return Result.Failure<StudentClassDetailResponse>(new Error(ErrorCodes.ClassAccessDenied, "You do not have an active or completed enrollment in this class."));
        var targetClass = await _context.Classes.AsNoTracking()
            .Include(item => item.Course).Include(item => item.Semester).Include(item => item.PrimaryLecturer)
            .FirstOrDefaultAsync(item => item.Id == classId, cancellationToken);
        if (targetClass == null ||
            targetClass.Status == ClassStatus.Archived && enrollmentStatus != EnrollmentStatus.Completed)
            return Result.Failure<StudentClassDetailResponse>(new Error(ErrorCodes.ClassAccessDenied, "This class is not available."));
        var rosterStatus = enrollmentStatus == EnrollmentStatus.Completed
            ? EnrollmentStatus.Completed
            : EnrollmentStatus.Active;
        var members = await _context.ClassStudents.AsNoTracking().Include(item => item.Student)
            .Where(item => item.ClassId == classId && item.EnrollmentStatus == rosterStatus)
            .OrderBy(item => item.Student.RollNumber)
            .Select(item => new StudentClassMemberDto
            {
                StudentId = item.StudentId, UserId = item.Student.UserId, RollNumber = item.Student.RollNumber ?? string.Empty,
                FullName = item.Student.FullName, Email = item.Student.Email, MajorCode = item.MajorCodeAtEnrollment,
                TeamId = item.TeamMembers.Where(member => member.CountsTowardActiveTeam).Select(member => (Guid?)member.TeamId).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        var teams = await TeamQuery().Where(item => item.ClassId == classId && item.Status == TeamStatus.Active).OrderBy(item => item.TeamCode).ToListAsync(cancellationToken);
        var mentorsByClass = await LoadMentorsByClassAsync([classId], cancellationToken);
        return Result.Success(new StudentClassDetailResponse
        {
            Class = MapClass(targetClass, mentorsByClass.GetValueOrDefault(classId) ?? Array.Empty<MentorSummaryDto>(), enrollmentStatus.Value),
            Students = members,
            Teams = teams.Select(TeamMappings.ToDto).ToArray()
        });
    }

    public async Task<Result<MyTeamResponse>> GetMyTeamAsync(
        Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var studentId = await ResolveStudentIdAsync(userId, role, cancellationToken);
        if (studentId.IsFailure) return Result.Failure<MyTeamResponse>(studentId.Error);
        var team = await TeamQuery()
            .Where(item => item.Status == TeamStatus.Active && item.TeamMembers.Any(member =>
                member.StudentId == studentId.Value && member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus == EnrollmentStatus.Active) &&
                (item.Class.Status == ClassStatus.Draft || item.Class.Status == ClassStatus.Active))
            .OrderByDescending(item => item.Class.Semester.Status == SemesterStatus.Active)
            .ThenByDescending(item => item.Class.Semester.Year)
            .FirstOrDefaultAsync(cancellationToken);
        if (team == null) return Result.Success(new MyTeamResponse());
        var dto = TeamMappings.ToDto(team);
        var mentorsByClass = await LoadMentorsByClassAsync([team.ClassId], cancellationToken);
        return Result.Success(new MyTeamResponse
        {
            Team = dto,
            Class = MapClass(team.Class, mentorsByClass.GetValueOrDefault(team.ClassId) ?? Array.Empty<MentorSummaryDto>(), EnrollmentStatus.Active),
            Members = dto.Members
        });
    }

    private async Task<Result<Guid>> ResolveClassIdAsync(string classIdentifier, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(classIdentifier, out var classId))
        {
            return Result.Success(classId);
        }

        if (!ClassSlugRules.TryNormalizeRouteSlug(classIdentifier, out var slug))
        {
            return Result.Failure<Guid>(new Error(ErrorCodes.ClassValidationError, "Class identifier must be a UUID or a valid class slug."));
        }

        var resolvedClassId = await _context.Classes
            .AsNoTracking()
            .Where(item => item.Slug == slug)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return resolvedClassId.HasValue
            ? Result.Success(resolvedClassId.Value)
            : Result.Failure<Guid>(new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
    }

    private async Task<Result<Guid>> ResolveStudentIdAsync(Guid userId, string role, CancellationToken cancellationToken)
    {
        if (!string.Equals(role, SystemRoles.Student, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<Guid>(new Error(ErrorCodes.ClassAccessDenied, "This endpoint is available only to students."));
        var studentId = await _context.Students.AsNoTracking().Where(item => item.UserId == userId).Select(item => (Guid?)item.Id).FirstOrDefaultAsync(cancellationToken);
        return studentId.HasValue
            ? Result.Success(studentId.Value)
            : Result.Failure<Guid>(new Error(ErrorCodes.ClassAccessDenied, "The account is not linked to a student profile."));
    }

    private async Task<Dictionary<Guid, IReadOnlyCollection<MentorSummaryDto>>> LoadMentorsByClassAsync(
        IReadOnlyCollection<Guid> classIds,
        CancellationToken cancellationToken)
    {
        if (classIds.Count == 0) return new Dictionary<Guid, IReadOnlyCollection<MentorSummaryDto>>();
        var rows = await _context.MentorAssignments.AsNoTracking()
            .Where(assignment =>
                classIds.Contains(assignment.Team.ClassId) &&
                assignment.Team.Status == TeamStatus.Active &&
                (assignment.Team.Class.Status == ClassStatus.Completed ||
                 assignment.Team.Class.Status == ClassStatus.Archived ||
                 assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null))
            .Select(assignment => new
            {
                ClassId = assignment.Team.ClassId,
                Mentor = new MentorSummaryDto
                {
                    MentorProfileId = assignment.MentorProfileId,
                    UserId = assignment.MentorProfile.UserId,
                    FullName = assignment.MentorProfile.User.FullName,
                    Email = assignment.MentorProfile.User.Email,
                    Organization = assignment.MentorProfile.Organization
                }
            })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(item => item.ClassId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<MentorSummaryDto>)group
                    .Select(item => item.Mentor)
                    .DistinctBy(item => item.MentorProfileId)
                    .ToArray());
    }

    private static StudentClassSummaryDto MapClass(
        Class item,
        IReadOnlyCollection<MentorSummaryDto> mentors,
        EnrollmentStatus enrollmentStatus)
    {
        return new StudentClassSummaryDto
        {
            Id = item.Id,
            Slug = item.Slug,
            ClassCode = item.ClassCode,
            SubjectCode = item.Course.Code,
            SubjectName = item.Course.Name,
            Semester = item.Semester.Code,
            Year = item.Semester.Year,
            ClassStatus = item.Status.ToString(),
            EnrollmentStatus = enrollmentStatus.ToString(),
            LectureId = item.PrimaryLecturer == null ? null : new StudentClassLecturerDto
            {
                Id = item.PrimaryLecturer.Id,
                Name = item.PrimaryLecturer.FullName,
                Email = item.PrimaryLecturer.Email
            },
            Mentors = mentors
        };
    }

    private IQueryable<Team> TeamQuery() => _context.Teams.AsNoTracking()
        .Include(item => item.Class).ThenInclude(item => item.Course)
        .Include(item => item.Class).ThenInclude(item => item.Semester)
        .Include(item => item.Class).ThenInclude(item => item.PrimaryLecturer)
        .Include(item => item.TeamMembers).ThenInclude(member => member.ClassStudent).ThenInclude(enrollment => enrollment.Student)
        .Include(item => item.MentorAssignments).ThenInclude(assignment => assignment.MentorProfile).ThenInclude(profile => profile.User)
        .Include(item => item.Project)
        .Include(item => item.ChatGroups);
}

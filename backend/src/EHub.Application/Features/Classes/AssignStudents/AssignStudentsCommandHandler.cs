using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.AssignStudents;

/// <summary>
/// Keeps class enrollment and team membership changes behind one authorization
/// and consistency boundary. Team creation remains in ManageTeams; this handler
/// only adds already-enrolled students to an existing team.
/// </summary>
public sealed class AssignStudentsCommandHandler : IAssignStudentsCommandHandler
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public AssignStudentsCommandHandler(IApplicationDbContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ClassStudentAssignmentResponse>> AssignToClassAsync(
        Guid classId,
        AssignStudentsToClassRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = NormalizeIds(request.StudentIds);
        if (requestedIds.Length == 0)
        {
            return ClassFailure(ErrorCodes.ClassAssignmentStudentsRequired, "Select at least one student to assign.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async token =>
            {
                var targetClass = await _context.Classes
                    .FirstOrDefaultAsync(item => item.Id == classId, token);
                if (targetClass == null)
                    return ClassFailure(ErrorCodes.ClassNotFound, "The requested class was not found.");

                var permission = ValidateManager(targetClass, currentUserId, currentUserRole);
                if (permission != null)
                    return ClassFailure(permission.Value.Code, permission.Value.Message);

                var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
                if (mutationError != null)
                    return ClassFailure(mutationError.Code, mutationError.Message);

                var students = await ResolveStudentsAsync(requestedIds, token);
                if (students == null)
                {
                    return ClassFailure(ErrorCodes.ClassAssignmentStudentNotFound,
                        "One or more selected students could not be found or are inactive.");
                }

                var studentIds = students.Select(item => item.Id).Distinct().ToArray();
                var existingEnrollments = await _context.ClassStudents
                    .Where(item => item.ClassId == classId && studentIds.Contains(item.StudentId))
                    .ToDictionaryAsync(item => item.StudentId, token);

                var conflictingEnrollment = await _context.ClassStudents
                    .Include(item => item.Class)
                    .AsNoTracking()
                    .Where(item => studentIds.Contains(item.StudentId) &&
                                   item.ClassId != classId &&
                                   item.SemesterId == targetClass.SemesterId &&
                                   item.CourseId == targetClass.CourseId &&
                                   item.CountsTowardCourseSemesterLimit)
                    .OrderBy(item => item.Class.ClassCode)
                    .FirstOrDefaultAsync(token);
                if (conflictingEnrollment != null)
                {
                    return ClassFailure(ErrorCodes.ClassStudentEnrollmentConflict,
                        $"Student is already enrolled in class '{conflictingEnrollment.Class.ClassCode}' for the same course and semester. Drop that enrollment before assigning the student here.");
                }

                var now = DateTime.UtcNow;
                foreach (var student in students)
                {
                    if (existingEnrollments.TryGetValue(student.Id, out var existing))
                    {
                        if (existing.EnrollmentStatus == EnrollmentStatus.Dropped)
                        {
                            existing.EnrollmentStatus = EnrollmentStatus.Active;
                            existing.CountsTowardCourseSemesterLimit = true;
                            existing.CompletedAtUtc = null;
                            existing.CompletedByUserId = null;
                            existing.UpdatedAt = now;
                            RecordEnrollmentActivity(classId, student.Id, currentUserId, now, "STUDENT_RE_ENROLLED", "Class.StudentReEnrolled.v1", "Assignment");
                        }

                        continue;
                    }

                    var major = StudentEnrollmentRules.ResolveEffectiveMajorCode(null, student.MajorCode);
                    if (!MajorCodes.IsValid(major))
                    {
                        return ClassFailure(ErrorCodes.ClassValidationError,
                            $"Student '{student.RollNumber ?? student.FullName}' has no valid registered major and cannot be assigned to a class.");
                    }

                    await _context.ClassStudents.AddAsync(new ClassStudent
                    {
                        ClassId = classId,
                        StudentId = student.Id,
                        SemesterId = targetClass.SemesterId,
                        CourseId = targetClass.CourseId,
                        EnrollmentStatus = EnrollmentStatus.Active,
                        CountsTowardCourseSemesterLimit = true,
                        MajorCodeAtEnrollment = major!,
                        MajorVerificationStatus = EnrollmentMajorVerificationStatus.Unverified,
                        CreatedAt = now,
                        UpdatedAt = now
                    }, token);
                    RecordEnrollmentActivity(classId, student.Id, currentUserId, now, "STUDENT_ENROLLMENT_ASSIGNED", "Class.StudentEnrollmentAdded.v1", "Assignment");
                }

                await _context.SaveChangesAsync(token);
                return Result.Success(new ClassStudentAssignmentResponse
                {
                    ClassId = classId,
                    AssignedStudentIds = studentIds
                });
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return ClassFailure(ErrorCodes.ClassConcurrencyConflict, "The class roster changed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return ClassFailure(ErrorCodes.ClassStudentEnrollmentConflict,
                "A selected student already has an enrollment for this course in the semester.");
        }
    }

    public async Task<Result<TeamStudentAssignmentResponse>> AssignToTeamAsync(
        Guid classId,
        Guid teamId,
        AssignStudentsToTeamRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = NormalizeIds(request.StudentIds);
        if (requestedIds.Length == 0)
        {
            return TeamFailure(ErrorCodes.TeamAssignmentStudentsRequired, "Select at least one student to assign.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(async token =>
            {
                var targetClass = await _context.Classes
                    .FirstOrDefaultAsync(item => item.Id == classId, token);
                if (targetClass == null)
                    return TeamFailure(ErrorCodes.ClassNotFound, "The requested class was not found.");

                var permission = ValidateManager(targetClass, currentUserId, currentUserRole);
                if (permission != null)
                    return TeamFailure(permission.Value.Code, permission.Value.Message);

                var mutationError = ClassStateRules.GetMutationError(targetClass.Status);
                if (mutationError != null)
                    return TeamFailure(mutationError.Code, mutationError.Message);

                var team = await _context.Teams
                    .Include(item => item.TeamMembers)
                    .ThenInclude(item => item.ClassStudent)
                    .ThenInclude(item => item.Student)
                    .FirstOrDefaultAsync(item => item.Id == teamId, token);
                if (team == null)
                    return TeamFailure(ErrorCodes.TeamNotFound, "The selected team was not found.");
                if (team.ClassId != classId)
                    return TeamFailure(ErrorCodes.TeamClassMismatch, "The selected team does not belong to this class.");
                if (team.Status != TeamStatus.Active)
                    return TeamFailure(ErrorCodes.TeamInactive, "Students cannot be assigned to an inactive team.");

                var enrollments = await ResolveActiveEnrollmentsAsync(classId, requestedIds, token);
                if (enrollments == null)
                {
                    return TeamFailure(ErrorCodes.TeamMemberNotInClass,
                        "Every selected student must have an active enrollment in this class before team assignment.");
                }

                var studentIds = enrollments.Select(item => item.StudentId).Distinct().ToArray();
                var conflictingMember = await _context.TeamMembers.AsNoTracking()
                    .AnyAsync(item => item.ClassId == classId && studentIds.Contains(item.StudentId) &&
                                      item.CountsTowardActiveTeam && item.TeamId != teamId, token);
                if (conflictingMember)
                {
                    return TeamFailure(ErrorCodes.TeamMembershipConflict,
                        "A selected student already belongs to another active team in this class.");
                }

                var activeMembers = team.TeamMembers.Where(item => item.CountsTowardActiveTeam).ToArray();
                var existingIds = activeMembers.Select(item => item.StudentId).ToHashSet();
                var additions = enrollments.Where(item => !existingIds.Contains(item.StudentId)).ToArray();
                if (activeMembers.Length + additions.Length > 6)
                {
                    return TeamFailure(ErrorCodes.TeamMemberLimitExceeded,
                        "This assignment would exceed the 6-student team limit.");
                }

                var now = DateTime.UtcNow;
                foreach (var enrollment in additions)
                {
                    team.TeamMembers.Add(new TeamMember
                    {
                        TeamId = team.Id,
                        Team = team,
                        ClassId = classId,
                        StudentId = enrollment.StudentId,
                        ClassStudent = enrollment,
                        RoleInTeam = TeamMemberRole.Member,
                        CountsTowardActiveTeam = true,
                        JoinedAt = now,
                        CreatedById = currentUserId
                    });
                }

                var resultingMembers = team.TeamMembers.Where(item => item.CountsTowardActiveTeam).ToArray();
                if (!resultingMembers.Any(item => item.RoleInTeam == TeamMemberRole.Leader) && resultingMembers.Length > 0)
                {
                    resultingMembers[0].RoleInTeam = TeamMemberRole.Leader;
                }

                team.UpdatedAt = now;
                team.UpdatedBy = currentUserId;
                var memberUserIds = resultingMembers
                    .Where(item => item.ClassStudent.Student.UserId.HasValue)
                    .Select(item => item.ClassStudent.Student.UserId!.Value)
                    .Distinct()
                    .ToArray();
                _context.ClassAuditLogs.Add(new ClassAuditLog
                {
                    ClassId = classId,
                    Action = "TEAM_MEMBERS_ASSIGNED",
                    PerformedByUserId = currentUserId,
                    OccurredAtUtc = now,
                    DetailsJson = JsonSerializer.Serialize(new { TeamId = team.Id, AssignedStudentIds = studentIds })
                });
                ClassOutbox.Enqueue(_context, "Team.MembersUpdated.v1", classId, new
                {
                    TeamId = team.Id,
                    team.TeamName,
                    MemberUserIds = memberUserIds
                }, now);

                await _context.SaveChangesAsync(token);
                return Result.Success(new TeamStudentAssignmentResponse
                {
                    ClassId = classId,
                    TeamId = team.Id,
                    AssignedStudentIds = studentIds
                });
            }, cancellationToken);
        }
        catch (SerializableTransactionConflictException)
        {
            return TeamFailure(ErrorCodes.ClassConcurrencyConflict, "The team changed concurrently. Refresh and try again.");
        }
        catch (DbUpdateException)
        {
            return TeamFailure(ErrorCodes.TeamMembershipConflict,
                "A selected student was assigned to another active team concurrently.");
        }
    }

    private async Task<IReadOnlyCollection<Student>?> ResolveStudentsAsync(
        IReadOnlyCollection<Guid> requestedIds,
        CancellationToken cancellationToken)
    {
        var students = await _context.Students
            .Where(item => item.Status == StudentStatus.Active &&
                           (requestedIds.Contains(item.Id) ||
                            (item.UserId.HasValue && requestedIds.Contains(item.UserId.Value))))
            .ToListAsync(cancellationToken);
        var resolved = requestedIds
            .Select(id => students.FirstOrDefault(item => item.Id == id || item.UserId == id))
            .ToArray();
        return resolved.Any(item => item == null)
            ? null
            : resolved.Select(item => item!).DistinctBy(item => item.Id).ToArray();
    }

    private async Task<IReadOnlyCollection<ClassStudent>?> ResolveActiveEnrollmentsAsync(
        Guid classId,
        IReadOnlyCollection<Guid> requestedIds,
        CancellationToken cancellationToken)
    {
        var enrollments = await _context.ClassStudents
            .Include(item => item.Student)
            .Where(item => item.ClassId == classId && item.EnrollmentStatus == EnrollmentStatus.Active &&
                           (requestedIds.Contains(item.StudentId) ||
                            (item.Student.UserId.HasValue && requestedIds.Contains(item.Student.UserId.Value))))
            .ToListAsync(cancellationToken);
        var resolved = requestedIds
            .Select(id => enrollments.FirstOrDefault(item => item.StudentId == id || item.Student.UserId == id))
            .ToArray();
        return resolved.Any(item => item == null)
            ? null
            : resolved.Select(item => item!).DistinctBy(item => item.StudentId).ToArray();
    }

    private void RecordEnrollmentActivity(
        Guid classId,
        Guid studentId,
        Guid userId,
        DateTime occurredAtUtc,
        string action,
        string eventType,
        string source)
    {
        _context.ClassAuditLogs.Add(new ClassAuditLog
        {
            ClassId = classId,
            Action = action,
            PerformedByUserId = userId,
            OccurredAtUtc = occurredAtUtc,
            DetailsJson = JsonSerializer.Serialize(new { StudentId = studentId, Source = source })
        });
        ClassOutbox.Enqueue(_context, eventType, classId, new { StudentId = studentId, Source = source }, occurredAtUtc);
    }

    private static Guid[] NormalizeIds(IReadOnlyCollection<Guid>? studentIds) =>
        (studentIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToArray();

    private static (string Code, string Message)? ValidateManager(
        Class targetClass,
        Guid userId,
        string role)
    {
        if (string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase) && targetClass.PrimaryLecturerId == userId)
            return null;
        return (ErrorCodes.ClassAccessDenied, "Only an administrator or the assigned lecturer can manage student assignments for this class.");
    }

    private static Result<ClassStudentAssignmentResponse> ClassFailure(string code, string message) =>
        Result.Failure<ClassStudentAssignmentResponse>(new Error(code, message));

    private static Result<TeamStudentAssignmentResponse> TeamFailure(string code, string message) =>
        Result.Failure<TeamStudentAssignmentResponse>(new Error(code, message));
}

using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.ManageTeachingStaff;

public sealed class SemesterTeachingStaffCommandHandler : ISemesterTeachingStaffCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SemesterTeachingStaffCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<TeachingStaffResponse>> AddAsync(
        AddSemesterTeachingStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator can manage semester teaching staff.");
        }

        if (!TryParseTerm(request.Semester, out var term) || request.Year is < 2000 or > 2100)
        {
            return Failure(ErrorCodes.ClassValidationError, "Semester and year are invalid.");
        }

        if (request.UserId == Guid.Empty || !TryParseRole(request.Role, out var role))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid staff member and role are required.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                async transactionCancellationToken =>
                {
                    var semester = await _context.Semesters
                        .FirstOrDefaultAsync(
                            item => item.Term == term && item.Year == request.Year,
                            transactionCancellationToken);

                    if (semester == null)
                    {
                        return Failure(ErrorCodes.SemesterNotFound, "Plan the semester before configuring its teaching staff.");
                    }

                    var lifecycleError = GetSemesterMutationError(semester);
                    if (lifecycleError != null)
                    {
                        return Failure(lifecycleError.Code, lifecycleError.Message);
                    }

                    var user = await LoadEligibleUserAsync(
                        request.UserId,
                        role,
                        transactionCancellationToken);
                    if (user == null)
                    {
                        return Failure(
                            ErrorCodes.SemesterStaffConflict,
                            $"The selected user is inactive or does not have {ToRoleCode(role)} role.");
                    }

                    var existing = await _context.SemesterStaffAssignments
                        .FirstOrDefaultAsync(
                            item =>
                                item.SemesterId == semester.Id &&
                                item.UserId == user.Id &&
                                item.Role == role,
                            transactionCancellationToken);
                    if (existing != null)
                    {
                        return Failure(
                            ErrorCodes.SemesterStaffConflict,
                            existing.Status == SemesterStaffStatus.Active
                                ? "This staff member is already in the semester teaching list."
                                : "This staff member already exists in the list. Edit the entry to reactivate it.");
                    }

                    var assignment = new SemesterStaffAssignment
                    {
                        SemesterId = semester.Id,
                        Semester = semester,
                        UserId = user.Id,
                        User = user,
                        Role = role,
                        Status = SemesterStaffStatus.Active,
                        CreatedBy = _currentUser.UserId
                    };

                    await _context.SemesterStaffAssignments.AddAsync(
                        assignment,
                        transactionCancellationToken);
                    AddAuditAndOutbox(
                        semester,
                        assignment,
                        "SEMESTER_STAFF_ADDED",
                        "Semester.StaffAdded.v1");
                    await _unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    return Result.Success(ToResponse(assignment));
                },
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure(
                ErrorCodes.SemesterStaffConflict,
                "The semester teaching list changed concurrently. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(
                ErrorCodes.SemesterStaffConflict,
                "The semester teaching list changed concurrently. Reload and try again.");
        }
    }

    public const int MaximumBatchSize = 200;

    public async Task<Result<AddSemesterTeachingStaffBatchResponse>> AddBatchAsync(
        AddSemesterTeachingStaffBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(
                ErrorCodes.ClassAccessDenied, "Only an administrator can manage semester teaching staff.");
        }

        if (!TryParseTerm(request.Semester, out var term) || request.Year is < 2000 or > 2100)
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(ErrorCodes.ClassValidationError, "Semester and year are invalid.");
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(ErrorCodes.ClassValidationError, "A valid role is required.");
        }

        var userIds = (request.UserIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (userIds.Length == 0)
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(ErrorCodes.ClassValidationError, "Select at least one staff member.");
        }

        if (userIds.Length > MaximumBatchSize)
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(
                ErrorCodes.ClassValidationError, $"At most {MaximumBatchSize} staff members can be added at once.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                async transactionCancellationToken =>
                {
                    var semester = await _context.Semesters
                        .FirstOrDefaultAsync(
                            item => item.Term == term && item.Year == request.Year,
                            transactionCancellationToken);
                    if (semester == null)
                    {
                        return Failure<AddSemesterTeachingStaffBatchResponse>(
                            ErrorCodes.SemesterNotFound, "Plan the semester before configuring its teaching staff.");
                    }

                    var lifecycleError = GetSemesterMutationError(semester);
                    if (lifecycleError != null)
                    {
                        return Failure<AddSemesterTeachingStaffBatchResponse>(lifecycleError.Code, lifecycleError.Message);
                    }

                    var users = await _context.Users
                        .Include(item => item.UserRoles)
                        .ThenInclude(item => item.Role)
                        .Where(item => userIds.Contains(item.Id))
                        .ToDictionaryAsync(item => item.Id, transactionCancellationToken);
                    var existing = await _context.SemesterStaffAssignments
                        .Where(item => item.SemesterId == semester.Id && item.Role == role && userIds.Contains(item.UserId))
                        .ToDictionaryAsync(item => item.UserId, transactionCancellationToken);

                    var results = new List<SemesterStaffBatchItemResponse>(userIds.Length);
                    foreach (var userId in userIds)
                    {
                        // A missing user and an ineligible one look the same to the caller.
                        if (!users.TryGetValue(userId, out var user) || !IsEligibleUser(user, role))
                        {
                            results.Add(new SemesterStaffBatchItemResponse
                            {
                                UserId = userId,
                                Outcome = SemesterStaffBatchOutcomes.Rejected,
                                Message = $"The selected user is inactive or does not have {ToRoleCode(role)} role."
                            });
                            continue;
                        }

                        if (existing.TryGetValue(userId, out var current))
                        {
                            results.Add(new SemesterStaffBatchItemResponse
                            {
                                UserId = userId,
                                Outcome = SemesterStaffBatchOutcomes.AlreadyInList,
                                Message = current.Status == SemesterStaffStatus.Active
                                    ? "This staff member is already in the semester teaching list."
                                    : "This staff member is in the list as inactive. Edit the entry to reactivate it.",
                                Staff = ToResponse(current, user)
                            });
                            continue;
                        }

                        var assignment = new SemesterStaffAssignment
                        {
                            SemesterId = semester.Id,
                            Semester = semester,
                            UserId = user.Id,
                            User = user,
                            Role = role,
                            Status = SemesterStaffStatus.Active,
                            CreatedBy = _currentUser.UserId
                        };
                        await _context.SemesterStaffAssignments.AddAsync(assignment, transactionCancellationToken);
                        AddAuditAndOutbox(semester, assignment, "SEMESTER_STAFF_ADDED", "Semester.StaffAdded.v1");
                        results.Add(new SemesterStaffBatchItemResponse
                        {
                            UserId = userId,
                            Outcome = SemesterStaffBatchOutcomes.Added,
                            Staff = ToResponse(assignment)
                        });
                    }

                    await _unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    return Result.Success(new AddSemesterTeachingStaffBatchResponse
                    {
                        Results = results,
                        AddedCount = results.Count(item => item.Outcome == SemesterStaffBatchOutcomes.Added),
                        AlreadyInListCount = results.Count(item => item.Outcome == SemesterStaffBatchOutcomes.AlreadyInList),
                        RejectedCount = results.Count(item => item.Outcome == SemesterStaffBatchOutcomes.Rejected)
                    });
                },
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(
                ErrorCodes.SemesterStaffConflict,
                "The semester teaching list changed concurrently. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure<AddSemesterTeachingStaffBatchResponse>(
                ErrorCodes.SemesterStaffConflict,
                "The semester teaching list changed concurrently. Reload and try again.");
        }
    }

    public async Task<Result<TeachingStaffResponse>> UpdateAsync(
        Guid assignmentId,
        UpdateSemesterTeachingStaffRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Failure(ErrorCodes.ClassAccessDenied, "Only an administrator can manage semester teaching staff.");
        }

        if (!uint.TryParse(request.RowVersion, out var expectedVersion))
        {
            return Failure(ErrorCodes.ClassValidationError, "A valid rowVersion is required.");
        }

        if (!TryParseStatus(request.Status, out var nextStatus))
        {
            return Failure(ErrorCodes.ClassValidationError, "Status must be Active or Inactive.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                async transactionCancellationToken =>
                {
                    var assignment = await _context.SemesterStaffAssignments
                        .Include(item => item.Semester)
                        .Include(item => item.User)
                        .ThenInclude(user => user.UserRoles)
                        .ThenInclude(userRole => userRole.Role)
                        .FirstOrDefaultAsync(
                            item => item.Id == assignmentId,
                            transactionCancellationToken);

                    if (assignment == null)
                    {
                        return Failure(ErrorCodes.SemesterStaffNotFound, "The semester teaching staff entry was not found.");
                    }

                    var lifecycleError = GetSemesterMutationError(assignment.Semester);
                    if (lifecycleError != null)
                    {
                        return Failure(lifecycleError.Code, lifecycleError.Message);
                    }

                    if (assignment.Version != expectedVersion)
                    {
                        return Failure(
                            ErrorCodes.SemesterConcurrencyConflict,
                            "The teaching staff entry changed concurrently. Reload and try again.");
                    }

                    if (assignment.Status == nextStatus)
                    {
                        return Result.Success(ToResponse(assignment));
                    }

                    if (nextStatus == SemesterStaffStatus.Active && !IsEligibleUser(assignment.User, assignment.Role))
                    {
                        return Failure(
                            ErrorCodes.SemesterStaffConflict,
                            $"The user must be active and have {ToRoleCode(assignment.Role)} role before reactivation.");
                    }

                    if (nextStatus == SemesterStaffStatus.Inactive)
                    {
                        var inUseMessage = await GetInUseMessageAsync(
                            assignment,
                            transactionCancellationToken);
                        if (inUseMessage != null)
                        {
                            return Failure(ErrorCodes.SemesterStaffInUse, inUseMessage);
                        }
                    }

                    assignment.Status = nextStatus;
                    assignment.UpdatedBy = _currentUser.UserId;
                    AddAuditAndOutbox(
                        assignment.Semester,
                        assignment,
                        nextStatus == SemesterStaffStatus.Active
                            ? "SEMESTER_STAFF_REACTIVATED"
                            : "SEMESTER_STAFF_DEACTIVATED",
                        nextStatus == SemesterStaffStatus.Active
                            ? "Semester.StaffReactivated.v1"
                            : "Semester.StaffDeactivated.v1");
                    await _unitOfWork.SaveChangesAsync(transactionCancellationToken);

                    return Result.Success(ToResponse(assignment));
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(
                ErrorCodes.SemesterConcurrencyConflict,
                "The teaching staff entry changed concurrently. Reload and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure(
                ErrorCodes.SemesterStaffConflict,
                "The semester teaching list conflicts with current academic data. Reload and try again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure(
                ErrorCodes.SemesterConcurrencyConflict,
                "The semester teaching list changed concurrently. Reload and try again.");
        }
    }

    public async Task<Result<MentorCarryoverPreviewResponse>> PreviewMentorCarryoverAsync(
        PreviewMentorCarryoverRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Failure<MentorCarryoverPreviewResponse>(
                ErrorCodes.ClassAccessDenied,
                "Only an administrator can reuse mentors across semesters.");
        }

        if (request.SourceSemesterId == Guid.Empty ||
            request.TargetSemesterId == Guid.Empty ||
            request.SourceSemesterId == request.TargetSemesterId)
        {
            return Failure<MentorCarryoverPreviewResponse>(
                ErrorCodes.ClassValidationError,
                "Different source and target semesters are required.");
        }

        var semesters = await _context.Semesters
            .AsNoTracking()
            .Where(item => item.Id == request.SourceSemesterId || item.Id == request.TargetSemesterId)
            .ToListAsync(cancellationToken);
        var sourceSemester = semesters.SingleOrDefault(item => item.Id == request.SourceSemesterId);
        var targetSemester = semesters.SingleOrDefault(item => item.Id == request.TargetSemesterId);
        if (sourceSemester == null || targetSemester == null)
        {
            return Failure<MentorCarryoverPreviewResponse>(
                ErrorCodes.SemesterNotFound,
                "The source or target semester was not found.");
        }
        if (!IsEarlierSemester(sourceSemester, targetSemester))
        {
            return Failure<MentorCarryoverPreviewResponse>(
                ErrorCodes.ClassValidationError,
                "The source semester must be earlier than the target semester.");
        }

        var lifecycleError = GetSemesterMutationError(targetSemester);
        if (lifecycleError != null)
        {
            return Failure<MentorCarryoverPreviewResponse>(lifecycleError.Code, lifecycleError.Message);
        }

        var sourceAssignments = await _context.SemesterStaffAssignments
            .AsNoTracking()
            .Include(item => item.User)
            .ThenInclude(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Include(item => item.User)
            .ThenInclude(user => user.MentorProfile)
            .Where(item =>
                item.SemesterId == sourceSemester.Id &&
                item.Role == SemesterStaffRole.Mentor)
            .OrderBy(item => item.User.FullName)
            .ToListAsync(cancellationToken);

        var targetAssignments = await _context.SemesterStaffAssignments
            .AsNoTracking()
            .Where(item =>
                item.SemesterId == targetSemester.Id &&
                item.Role == SemesterStaffRole.Mentor)
            .ToDictionaryAsync(item => item.UserId, cancellationToken);

        var candidates = sourceAssignments.Select(sourceAssignment =>
        {
            targetAssignments.TryGetValue(sourceAssignment.UserId, out var targetAssignment);
            return ToCarryoverCandidate(sourceAssignment, targetAssignment);
        }).ToArray();

        return Result.Success(new MentorCarryoverPreviewResponse
        {
            SourceSemesterId = sourceSemester.Id,
            TargetSemesterId = targetSemester.Id,
            TotalCount = candidates.Length,
            EligibleCount = candidates.Count(item => item.CanSelect),
            AlreadyAddedCount = candidates.Count(item => item.Action == "AlreadyAdded"),
            UnavailableCount = candidates.Count(item => item.Action == "Unavailable"),
            EnterpriseCount = candidates.Count(item => item.MentorType == MentorType.Enterprise.ToString()),
            AcademicCount = candidates.Count(item => item.MentorType == MentorType.Academic.ToString()),
            Mentors = candidates
        });
    }

    public async Task<Result<MentorCarryoverCommitResponse>> CommitMentorCarryoverAsync(
        CommitMentorCarryoverRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin())
        {
            return Failure<MentorCarryoverCommitResponse>(
                ErrorCodes.ClassAccessDenied,
                "Only an administrator can reuse mentors across semesters.");
        }

        var selectedUserIds = request.MentorUserIds.Distinct().ToArray();
        if (request.SourceSemesterId == Guid.Empty ||
            request.TargetSemesterId == Guid.Empty ||
            request.SourceSemesterId == request.TargetSemesterId ||
            selectedUserIds.Length == 0 ||
            selectedUserIds.Length > 500 ||
            selectedUserIds.Any(item => item == Guid.Empty) ||
            selectedUserIds.Length != request.MentorUserIds.Count)
        {
            return Failure<MentorCarryoverCommitResponse>(
                ErrorCodes.ClassValidationError,
                "Select between 1 and 500 distinct mentors and use different source and target semesters.");
        }

        try
        {
            return await _unitOfWork.ExecuteInSerializableTransactionAsync(
                async transactionCancellationToken =>
                {
                    var semesters = await _context.Semesters
                        .Where(item => item.Id == request.SourceSemesterId || item.Id == request.TargetSemesterId)
                        .ToListAsync(transactionCancellationToken);
                    var sourceSemester = semesters.SingleOrDefault(item => item.Id == request.SourceSemesterId);
                    var targetSemester = semesters.SingleOrDefault(item => item.Id == request.TargetSemesterId);
                    if (sourceSemester == null || targetSemester == null)
                    {
                        return Failure<MentorCarryoverCommitResponse>(
                            ErrorCodes.SemesterNotFound,
                            "The source or target semester was not found.");
                    }
                    if (!IsEarlierSemester(sourceSemester, targetSemester))
                    {
                        return Failure<MentorCarryoverCommitResponse>(
                            ErrorCodes.ClassValidationError,
                            "The source semester must be earlier than the target semester.");
                    }

                    var lifecycleError = GetSemesterMutationError(targetSemester);
                    if (lifecycleError != null)
                    {
                        return Failure<MentorCarryoverCommitResponse>(lifecycleError.Code, lifecycleError.Message);
                    }

                    var sourceAssignments = await _context.SemesterStaffAssignments
                        .Include(item => item.User)
                        .ThenInclude(user => user.UserRoles)
                        .ThenInclude(userRole => userRole.Role)
                        .Include(item => item.User)
                        .ThenInclude(user => user.MentorProfile)
                        .Where(item =>
                            item.SemesterId == sourceSemester.Id &&
                            item.Role == SemesterStaffRole.Mentor &&
                            selectedUserIds.Contains(item.UserId))
                        .ToListAsync(transactionCancellationToken);

                    if (sourceAssignments.Count != selectedUserIds.Length)
                    {
                        return Failure<MentorCarryoverCommitResponse>(
                            ErrorCodes.SemesterStaffConflict,
                            "One or more selected mentors no longer belong to the source semester. Preview again.");
                    }

                    var unavailable = sourceAssignments.FirstOrDefault(item => !IsEligibleSourceMentor(item));
                    if (unavailable != null)
                    {
                        return Failure<MentorCarryoverCommitResponse>(
                            ErrorCodes.SemesterStaffConflict,
                            $"{unavailable.User.FullName} is no longer eligible for reuse. Preview again.");
                    }

                    var targetAssignments = await _context.SemesterStaffAssignments
                        .Where(item =>
                            item.SemesterId == targetSemester.Id &&
                            item.Role == SemesterStaffRole.Mentor &&
                            selectedUserIds.Contains(item.UserId))
                        .ToDictionaryAsync(item => item.UserId, transactionCancellationToken);

                    var added = 0;
                    var reactivated = 0;
                    var alreadyAdded = 0;
                    foreach (var sourceAssignment in sourceAssignments)
                    {
                        if (targetAssignments.TryGetValue(sourceAssignment.UserId, out var targetAssignment))
                        {
                            if (targetAssignment.Status == SemesterStaffStatus.Active)
                            {
                                alreadyAdded++;
                                continue;
                            }

                            targetAssignment.Status = SemesterStaffStatus.Active;
                            targetAssignment.UpdatedBy = _currentUser.UserId;
                            reactivated++;
                            continue;
                        }

                        var assignment = new SemesterStaffAssignment
                        {
                            SemesterId = targetSemester.Id,
                            Semester = targetSemester,
                            UserId = sourceAssignment.UserId,
                            User = sourceAssignment.User,
                            Role = SemesterStaffRole.Mentor,
                            Status = SemesterStaffStatus.Active,
                            CreatedBy = _currentUser.UserId
                        };
                        await _context.SemesterStaffAssignments.AddAsync(assignment, transactionCancellationToken);
                        added++;
                    }

                    if (added > 0 || reactivated > 0)
                    {
                        AddMentorCarryoverAuditAndOutbox(
                            sourceSemester,
                            targetSemester,
                            selectedUserIds,
                            added,
                            reactivated,
                            alreadyAdded);
                        await _unitOfWork.SaveChangesAsync(transactionCancellationToken);
                    }

                    return Result.Success(new MentorCarryoverCommitResponse
                    {
                        AddedCount = added,
                        ReactivatedCount = reactivated,
                        AlreadyAddedCount = alreadyAdded
                    });
                },
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure<MentorCarryoverCommitResponse>(
                ErrorCodes.SemesterConcurrencyConflict,
                "The target semester mentor list changed concurrently. Preview and try again.");
        }
        catch (DbUpdateException)
        {
            return Failure<MentorCarryoverCommitResponse>(
                ErrorCodes.SemesterStaffConflict,
                "The target semester mentor list changed while mentors were being reused. Preview again.");
        }
        catch (SerializableTransactionConflictException)
        {
            return Failure<MentorCarryoverCommitResponse>(
                ErrorCodes.SemesterConcurrencyConflict,
                "The target semester mentor list changed concurrently. Preview and try again.");
        }
    }

    private async Task<string?> GetInUseMessageAsync(
        SemesterStaffAssignment assignment,
        CancellationToken cancellationToken)
    {
        if (assignment.Role == SemesterStaffRole.Lecturer)
        {
            var assignedClassCount = await _context.Classes
                .AsNoTracking()
                .CountAsync(
                    item =>
                        item.SemesterId == assignment.SemesterId &&
                        item.PrimaryLecturerId == assignment.UserId &&
                        (item.Status == ClassStatus.Draft ||
                         item.Status == ClassStatus.Active ||
                         item.Status == ClassStatus.Inactive),
                    cancellationToken);

            return assignedClassCount == 0
                ? null
                : $"Reassign this lecturer from {assignedClassCount} operational class(es) before deactivating the semester entry.";
        }

        var activeMentorAssignmentCount = await _context.MentorAssignments
            .AsNoTracking()
            .CountAsync(
                item =>
                    item.MentorProfile.UserId == assignment.UserId &&
                    item.Team.Class.SemesterId == assignment.SemesterId &&
                    item.Status == MentorAssignmentStatus.Active &&
                    item.EndedAt == null,
                cancellationToken);

        return activeMentorAssignmentCount == 0
            ? null
            : $"End or reassign this mentor from {activeMentorAssignmentCount} active team assignment(s) before deactivating the semester entry.";
    }

    private async Task<User?> LoadEligibleUserAsync(
        Guid userId,
        SemesterStaffRole role,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .Include(item => item.UserRoles)
            .ThenInclude(item => item.Role)
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        return user != null && IsEligibleUser(user, role)
            ? user
            : null;
    }

    private static bool IsEligibleUser(User user, SemesterStaffRole role)
    {
        var expectedRole = role == SemesterStaffRole.Lecturer
            ? SystemRoles.Lecturer
            : SystemRoles.Mentor;

        return user.Status == UserStatus.Active && user.UserRoles.Any(item =>
            string.Equals(item.Role.Name, expectedRole, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEligibleSourceMentor(SemesterStaffAssignment assignment) =>
        assignment.Status == SemesterStaffStatus.Active &&
        assignment.User.Status == UserStatus.Active &&
        assignment.User.MentorProfile is { Status: MentorProfileStatus.Active } &&
        assignment.User.UserRoles.Any(item =>
            string.Equals(item.Role.Name, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase));

    private static MentorCarryoverCandidateResponse ToCarryoverCandidate(
        SemesterStaffAssignment sourceAssignment,
        SemesterStaffAssignment? targetAssignment)
    {
        var profile = sourceAssignment.User.MentorProfile;
        var action = "Add";
        var canSelect = true;
        var message = "Ready to add to the target semester.";

        if (sourceAssignment.Status != SemesterStaffStatus.Active)
        {
            action = "Unavailable";
            canSelect = false;
            message = "Inactive in the source semester.";
        }
        else if (sourceAssignment.User.Status != UserStatus.Active)
        {
            action = "Unavailable";
            canSelect = false;
            message = $"Account is {sourceAssignment.User.Status}.";
        }
        else if (profile is null || profile.Status != MentorProfileStatus.Active ||
                 !sourceAssignment.User.UserRoles.Any(item =>
                     string.Equals(item.Role.Name, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase)))
        {
            action = "Unavailable";
            canSelect = false;
            message = "Mentor profile or role is not active.";
        }
        else if (targetAssignment?.Status == SemesterStaffStatus.Active)
        {
            action = "AlreadyAdded";
            canSelect = false;
            message = "Already available in the target semester.";
        }
        else if (targetAssignment is not null)
        {
            action = "Reactivate";
            message = "Will be reactivated in the target semester.";
        }

        return new MentorCarryoverCandidateResponse
        {
            UserId = sourceAssignment.UserId,
            Name = sourceAssignment.User.FullName,
            Email = sourceAssignment.User.Email,
            Avatar = sourceAssignment.User.AvatarUrl,
            MentorType = profile?.Type.ToString() ?? string.Empty,
            Action = action,
            CanSelect = canSelect,
            Message = message
        };
    }

    private void AddAuditAndOutbox(
        Semester semester,
        SemesterStaffAssignment assignment,
        string action,
        string eventType)
    {
        var eventId = Guid.NewGuid();
        var occurredAtUtc = DateTime.UtcNow;
        var performedByUserId = _currentUser.UserId ?? Guid.Empty;
        var details = new
        {
            AssignmentId = assignment.Id,
            assignment.UserId,
            Role = ToRoleCode(assignment.Role),
            Status = assignment.Status.ToString()
        };

        _context.SemesterAuditLogs.Add(new SemesterAuditLog
        {
            SemesterId = semester.Id,
            Action = action,
            PerformedByUserId = performedByUserId,
            OccurredAtUtc = occurredAtUtc,
            DetailsJson = JsonSerializer.Serialize(details)
        });
        _context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = eventType,
            AggregateType = "Semester",
            AggregateId = semester.Id,
            OccurredAtUtc = occurredAtUtc,
            AvailableAtUtc = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                EventId = eventId,
                EventType = eventType,
                AggregateType = "Semester",
                AggregateId = semester.Id,
                OccurredAtUtc = occurredAtUtc,
                Data = details
            })
        });
    }

    private void AddMentorCarryoverAuditAndOutbox(
        Semester sourceSemester,
        Semester targetSemester,
        IReadOnlyCollection<Guid> mentorUserIds,
        int addedCount,
        int reactivatedCount,
        int alreadyAddedCount)
    {
        var eventId = Guid.NewGuid();
        var occurredAtUtc = DateTime.UtcNow;
        var performedByUserId = _currentUser.UserId ?? Guid.Empty;
        var details = new
        {
            SourceSemesterId = sourceSemester.Id,
            TargetSemesterId = targetSemester.Id,
            MentorUserIds = mentorUserIds,
            AddedCount = addedCount,
            ReactivatedCount = reactivatedCount,
            AlreadyAddedCount = alreadyAddedCount
        };

        _context.SemesterAuditLogs.Add(new SemesterAuditLog
        {
            SemesterId = targetSemester.Id,
            Action = "SEMESTER_MENTORS_CARRIED_OVER",
            PerformedByUserId = performedByUserId,
            OccurredAtUtc = occurredAtUtc,
            DetailsJson = JsonSerializer.Serialize(details)
        });
        _context.OutboxMessages.Add(new OutboxMessage
        {
            EventId = eventId,
            Type = "Semester.MentorsCarriedOver.v1",
            AggregateType = "Semester",
            AggregateId = targetSemester.Id,
            OccurredAtUtc = occurredAtUtc,
            AvailableAtUtc = occurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(new
            {
                EventId = eventId,
                EventType = "Semester.MentorsCarriedOver.v1",
                AggregateType = "Semester",
                AggregateId = targetSemester.Id,
                OccurredAtUtc = occurredAtUtc,
                Data = details
            }, JsonOptions)
        });
    }

    private static Error? GetSemesterMutationError(Semester semester) => semester.Status switch
    {
        SemesterStatus.Closing => new Error(
            ErrorCodes.SemesterInvalidState,
            "Teaching staff of a closing semester cannot be changed."),
        SemesterStatus.Completed => new Error(
            ErrorCodes.SemesterInvalidState,
            "Teaching staff of a completed semester cannot be changed."),
        SemesterStatus.Archived => new Error(
            ErrorCodes.SemesterInvalidState,
            "Teaching staff of an archived semester cannot be changed."),
        _ => null
    };

    private static bool IsEarlierSemester(Semester source, Semester target) =>
        SemesterOrder(source) < SemesterOrder(target);

    private static int SemesterOrder(Semester semester)
    {
        var termOrder = semester.Term switch
        {
            SemesterTerm.Spring => 0,
            SemesterTerm.Summer => 1,
            SemesterTerm.Fall => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(semester.Term))
        };
        return semester.Year * 3 + termOrder;
    }

    private static bool TryParseTerm(string? value, out SemesterTerm term)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        term = normalized switch
        {
            "SP" => SemesterTerm.Spring,
            "SU" => SemesterTerm.Summer,
            "FA" => SemesterTerm.Fall,
            _ => default
        };

        return normalized is "SP" or "SU" or "FA";
    }

    private static bool TryParseRole(string? value, out SemesterStaffRole role)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        role = normalized switch
        {
            "LECTURER" => SemesterStaffRole.Lecturer,
            "MENTOR" => SemesterStaffRole.Mentor,
            _ => default
        };

        return normalized is "LECTURER" or "MENTOR";
    }

    private static bool TryParseStatus(string? value, out SemesterStaffStatus status)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        status = normalized switch
        {
            "ACTIVE" => SemesterStaffStatus.Active,
            "INACTIVE" => SemesterStaffStatus.Inactive,
            _ => default
        };

        return normalized is "ACTIVE" or "INACTIVE";
    }

    private static string ToRoleCode(SemesterStaffRole role) => role switch
    {
        SemesterStaffRole.Lecturer => "LECTURER",
        SemesterStaffRole.Mentor => "MENTOR",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static TeachingStaffResponse ToResponse(SemesterStaffAssignment assignment, User user) => new()
    {
        Id = assignment.Id,
        UserId = assignment.UserId,
        Name = user.FullName,
        Email = user.Email,
        Avatar = user.AvatarUrl,
        Role = ToRoleCode(assignment.Role),
        Status = assignment.Status.ToString(),
        UserStatus = user.Status.ToString(),
        RowVersion = assignment.Version.ToString()
    };

    private static TeachingStaffResponse ToResponse(SemesterStaffAssignment assignment) => new()
    {
        Id = assignment.Id,
        UserId = assignment.UserId,
        Name = assignment.User.FullName,
        Email = assignment.User.Email,
        Avatar = assignment.User.AvatarUrl,
        Role = ToRoleCode(assignment.Role),
        Status = assignment.Status.ToString(),
        UserStatus = assignment.User.Status.ToString(),
        RowVersion = assignment.Version.ToString()
    };

    private static Result<TeachingStaffResponse> Failure(string code, string message) =>
        Result.Failure<TeachingStaffResponse>(new Error(code, message));

    private static Result<T> Failure<T>(string code, string message) =>
        Result.Failure<T>(new Error(code, message));

    private bool IsAdmin() => _currentUser.Roles.Any(role =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase));
}

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

    private static Error? GetSemesterMutationError(Semester semester) => semester.Status switch
    {
        SemesterStatus.Completed => new Error(
            ErrorCodes.SemesterInvalidState,
            "Teaching staff of a completed semester cannot be changed."),
        SemesterStatus.Archived => new Error(
            ErrorCodes.SemesterInvalidState,
            "Teaching staff of an archived semester cannot be changed."),
        _ => null
    };

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

    private bool IsAdmin() => _currentUser.Roles.Any(role =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase));
}

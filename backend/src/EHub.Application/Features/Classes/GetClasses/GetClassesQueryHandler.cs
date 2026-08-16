using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.GetClasses;

public sealed class GetClassesQueryHandler : IGetClassesQueryHandler
{
    private readonly IApplicationDbContext _context;

    public GetClassesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassListResponse>> HandleAsync(
        GetClassesRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        // 1. Validation
        if (request.Page < 1)
        {
            return Result.Failure<ClassListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Page number must be greater than 0."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<ClassListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Page size must be between 1 and 100."));
        }

        // 2. Ownership & Authorization Filter
        var isAdmin = ClassAuthorizationRules.IsAdmin(currentUserRole);
        var isLecturer = ClassAuthorizationRules.IsLecturer(currentUserRole);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassListResponse>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have permission to view class list."));
        }

        bool? mustBeAssigned = null;
        if (!string.IsNullOrWhiteSpace(request.AssignmentStatus))
        {
            var assignmentStatus = request.AssignmentStatus.Trim();
            if (assignmentStatus.Equals("Assigned", StringComparison.OrdinalIgnoreCase))
            {
                mustBeAssigned = true;
            }
            else if (assignmentStatus.Equals("Unassigned", StringComparison.OrdinalIgnoreCase))
            {
                mustBeAssigned = false;
            }
            else
            {
                return Result.Failure<ClassListResponse>(
                    new Error(ErrorCodes.ClassValidationError, "Assignment status must be Assigned or Unassigned."));
            }
        }

        var query = _context.Classes.AsNoTracking();
        if (isLecturer)
        {
            query = query.Where(@class => @class.PrimaryLecturerId == currentUserId);
        }

        if (mustBeAssigned.HasValue)
        {
            query = mustBeAssigned.Value
                ? query.Where(@class => @class.PrimaryLecturerId != null)
                : query.Where(@class => @class.PrimaryLecturerId == null);
        }

        // 3. Status Filter (Mặc định là Active)
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(c => c.Status == ClassStatus.Draft || c.Status == ClassStatus.Active);
        }
        else if (Enum.TryParse<ClassStatus>(request.Status, true, out var requestedStatus))
        {
            query = query.Where(c => c.Status == requestedStatus);
        }
        else
        {
            return Result.Failure<ClassListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Status filter must be Draft, Active, Inactive, Completed, or Archived."));
        }

        // 4. AcademicTerm Filter
        if (request.SemesterId.HasValue)
        {
            query = query.Where(c => c.SemesterId == request.SemesterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SemesterCode))
        {
            var semCode = request.SemesterCode.Trim().ToUpperInvariant();
            query = query.Where(c => c.Semester.Code == semCode);
        }

        if (request.Year.HasValue)
        {
            query = query.Where(c => c.Semester.Year == request.Year.Value);
        }

        // 5. Subject Filter
        if (request.CourseId.HasValue)
        {
            query = query.Where(c => c.CourseId == request.CourseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.SubjectCode))
        {
            var subjCode = request.SubjectCode.Trim().ToUpperInvariant();
            query = query.Where(c => c.Course.Code == subjCode);
        }

        // 6. Search Filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.Trim().ToLower();
            if (searchTerm.Length > 100)
            {
                searchTerm = searchTerm[..100];
            }

            query = query.Where(c => c.ClassCode.ToLower().Contains(searchTerm) ||
                                     c.Course.Code.ToLower().Contains(searchTerm) ||
                                     c.Course.Name.ToLower().Contains(searchTerm));
        }

        // 7. Count Total
        var totalCount = await query.CountAsync(cancellationToken);

        // 8. Sorting Whitelist
        var normalizedSort = request.Sort?.Trim().ToLowerInvariant() ?? "code";
        var allowedSorts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "code", "classcode", "-code", "-classcode",
            "createdat", "-createdat", "classindex", "-classindex"
        };
        if (!allowedSorts.Contains(normalizedSort))
        {
            return Result.Failure<ClassListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Sort must be code, createdAt, classIndex, or the descending '-' variant."));
        }

        query = normalizedSort switch
        {
            "-code" or "-classcode" => query.OrderByDescending(c => c.ClassCode),
            "createdat" => query.OrderBy(c => c.CreatedAt),
            "-createdat" => query.OrderByDescending(c => c.CreatedAt),
            "classindex" => query.OrderBy(c => c.ClassIndex),
            "-classindex" => query.OrderByDescending(c => c.ClassIndex),
            _ => query.OrderBy(c => c.ClassCode)
        };

        // 9. Pagination & Projection
        var projectedItems = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new
            {
                c.Id,
                c.ClassCode,
                c.ClassIndex,
                c.CourseId,
                SubjectCode = c.Course.Code,
                SubjectName = c.Course.Name,
                c.SemesterId,
                SemesterCode = c.Semester.Code,
                c.Semester.Year,
                c.PrimaryLecturerId,
                PrimaryLecturerName = c.PrimaryLecturer != null ? c.PrimaryLecturer.FullName : null,
                PrimaryLecturerEmail = c.PrimaryLecturer != null ? c.PrimaryLecturer.Email : null,
                c.Room,
                c.ScheduleJson,
                c.IsEnrollmentMajorLocked,
                c.Status,
                c.StatusBeforeArchive,
                StudentCount = c.ClassStudents.Count(cs =>
                    c.Status == ClassStatus.Completed ||
                    c.Status == ClassStatus.Archived && c.StatusBeforeArchive == ClassStatus.Completed
                        ? cs.EnrollmentStatus == EnrollmentStatus.Completed
                        : cs.EnrollmentStatus == EnrollmentStatus.Active),
                TeamCount = c.Teams.Count(t => t.Status == TeamStatus.Active),
                c.CreatedAt,
                c.CompletedAtUtc,
                c.CompletionReason,
                c.Version
            })
            .ToListAsync(cancellationToken);

        var pageClassIds = projectedItems.Select(item => item.Id).ToArray();
        var mentorRows = await _context.MentorAssignments
            .AsNoTracking()
            .Where(assignment =>
                pageClassIds.Contains(assignment.Team.ClassId) &&
                assignment.Team.Status == TeamStatus.Active &&
                (assignment.Team.Class.Status == ClassStatus.Completed ||
                 assignment.Team.Class.Status == ClassStatus.Archived &&
                 assignment.Team.Class.StatusBeforeArchive == ClassStatus.Completed ||
                 assignment.Status == MentorAssignmentStatus.Active && assignment.EndedAt == null))
            .Select(assignment => new
            {
                ClassId = assignment.Team.ClassId,
                assignment.MentorProfileId,
                assignment.MentorProfile.UserId,
                assignment.MentorProfile.User.FullName,
                assignment.MentorProfile.User.Email
            })
            .Distinct()
            .ToListAsync(cancellationToken);
        var mentorsByClass = mentorRows
            .GroupBy(item => item.ClassId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => new ClassMentorSummaryDto
                {
                    MentorProfileId = item.MentorProfileId,
                    UserId = item.UserId,
                    FullName = item.FullName,
                    Email = item.Email
                }).ToArray());

        var items = projectedItems.Select(c => new ClassResponse
        {
            Id = c.Id,
            ClassCode = c.ClassCode,
            ClassIndex = c.ClassIndex,
            CourseId = c.CourseId,
            SubjectCode = c.SubjectCode,
            SubjectName = c.SubjectName,
            SemesterId = c.SemesterId,
            SemesterCode = c.SemesterCode,
            Year = c.Year,
            PrimaryLecturerId = c.PrimaryLecturerId,
            PrimaryLecturerName = c.PrimaryLecturerName,
            PrimaryLecturerEmail = c.PrimaryLecturerEmail,
            Room = c.Room,
            Schedules = ClassScheduleRules.Deserialize(c.ScheduleJson),
            IsEnrollmentMajorLocked = !ClassStateRules.IsReadOnly(c.Status) && c.IsEnrollmentMajorLocked,
            Status = c.Status.ToString(),
            StatusBeforeArchive = c.StatusBeforeArchive?.ToString(),
            StudentCount = c.StudentCount,
            TeamCount = c.TeamCount,
            Mentors = mentorsByClass.GetValueOrDefault(c.Id) ?? Array.Empty<ClassMentorSummaryDto>(),
            CreatedAtUtc = c.CreatedAt,
            CompletedAtUtc = c.CompletedAtUtc,
            CompletionReason = c.CompletionReason,
            RowVersion = c.Version.ToString()
        }).ToArray();

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)request.PageSize));

        var response = new ClassListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalPages = totalPages
        };

        return Result.Success(response);
    }
}

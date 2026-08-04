using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Classes;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
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
                new Error("Classes.InvalidPage", "Page number must be greater than 0."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<ClassListResponse>(
                new Error("Classes.InvalidPageSize", "Page size must be between 1 and 100."));
        }

        // 2. Ownership & Authorization Filter
        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassListResponse>(
                new Error("Classes.AccessDenied", "You do not have permission to view class list."));
        }

        var query = _context.Classes.AsNoTracking();

        if (isLecturer)
        {
            query = query.Where(c => c.PrimaryLecturerId == currentUserId ||
                                     c.ClassLecturers.Any(cl => cl.LecturerId == currentUserId));
        }

        // 3. Status Filter (Mặc định là Active)
        if (string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(c => c.Status == ClassStatus.Active);
        }
        else if (request.Status.Equals("Archived", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.Status == ClassStatus.Archived);
        }
        else if (request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.Status == ClassStatus.Active);
        }
        else
        {
            return Result.Failure<ClassListResponse>(
                new Error("Classes.InvalidStatus", "Status filter must be 'Active' or 'Archived'."));
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
        query = request.Sort?.Trim().ToLowerInvariant() switch
        {
            "-code" or "-classcode" => query.OrderByDescending(c => c.ClassCode),
            "createdat" => query.OrderBy(c => c.CreatedAt),
            "-createdat" => query.OrderByDescending(c => c.CreatedAt),
            "classindex" => query.OrderBy(c => c.ClassIndex),
            "-classindex" => query.OrderByDescending(c => c.ClassIndex),
            _ => query.OrderBy(c => c.ClassCode)
        };

        // 9. Pagination & Projection
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new ClassResponse
            {
                Id = c.Id,
                ClassCode = c.ClassCode,
                ClassIndex = c.ClassIndex,
                CourseId = c.CourseId,
                SubjectCode = c.Course.Code,
                SubjectName = c.Course.Name,
                SemesterId = c.SemesterId,
                SemesterCode = c.Semester.Code,
                Year = c.Semester.Year,
                PrimaryLecturerId = c.PrimaryLecturerId,
                PrimaryLecturerName = c.PrimaryLecturer != null ? c.PrimaryLecturer.FullName : null,
                PrimaryLecturerEmail = c.PrimaryLecturer != null ? c.PrimaryLecturer.Email : null,
                Room = c.Room,
                ScheduleJson = c.ScheduleJson,
                IsEnrollmentMajorLocked = c.IsEnrollmentMajorLocked,
                Status = c.Status.ToString(),
                StudentCount = c.ClassStudents.Count(cs => cs.EnrollmentStatus == EnrollmentStatus.Active),
                TeamCount = c.Teams.Count(t => t.Status == TeamStatus.Active),
                CreatedAtUtc = c.CreatedAt
            })
            .ToListAsync(cancellationToken);

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

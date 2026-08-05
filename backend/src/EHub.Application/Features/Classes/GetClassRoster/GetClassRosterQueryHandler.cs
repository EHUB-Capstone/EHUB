using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.Common;
using EHub.Contracts.Classes;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Classes.GetClassRoster;

public sealed class GetClassRosterQueryHandler : IGetClassRosterQueryHandler
{
    private readonly IApplicationDbContext _context;

    public GetClassRosterQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ClassRosterListResponse>> HandleAsync(
        Guid classId,
        GetClassRosterRequest request,
        Guid currentUserId,
        string currentUserRole,
        CancellationToken cancellationToken = default)
    {
        if (request.Page < 1)
        {
            return Result.Failure<ClassRosterListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Page number must be greater than 0."));
        }

        if (request.PageSize is < 1 or > 100)
        {
            return Result.Failure<ClassRosterListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Page size must be between 1 and 100."));
        }

        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isLecturer)
        {
            return Result.Failure<ClassRosterListResponse>(
                new Error(ErrorCodes.ClassAccessDenied, "You do not have permission to view this class roster."));
        }

        var targetClass = await _context.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassRosterListResponse>(
                new Error(ErrorCodes.ClassNotFound, "The requested class was not found."));
        }

        if (isLecturer)
        {
            if (targetClass.PrimaryLecturerId != currentUserId)
            {
                return Result.Failure<ClassRosterListResponse>(
                    new Error(ErrorCodes.ClassAccessDenied, "You can only view roster for classes assigned to you."));
            }
        }

        var page = request.Page;
        var pageSize = request.PageSize;

        if (!ClassRosterFilters.TryParseStatus(request.Status, out var status))
        {
            return Result.Failure<ClassRosterListResponse>(
                new Error(ErrorCodes.ClassValidationError, "Enrollment status must be Active, Dropped, or Completed."));
        }

        var query = _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Include(cs => cs.TeamMembers)
            .ThenInclude(tm => tm.Team)
            .Where(cs => cs.ClassId == classId);

        query = ClassRosterFilters.Apply(query, request.Search, request.MajorCode, status);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

        var classStudents = await query
            .OrderBy(cs => cs.Student.RollNumber)
            .ThenBy(cs => cs.Student.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = classStudents.Select(cs =>
        {
            var activeTeamMember = cs.TeamMembers.FirstOrDefault(tm => tm.CountsTowardActiveTeam && tm.Team != null && tm.Team.Status == TeamStatus.Active);
            return new ClassStudentDto
            {
                StudentId = cs.StudentId,
                RollNumber = cs.Student.RollNumber ?? string.Empty,
                FullName = cs.Student.FullName,
                Email = cs.Student.Email ?? string.Empty,
                MajorCode = cs.MajorCodeAtEnrollment,
                ProfileMajorCode = cs.Student.MajorCode,
                MajorVerificationStatus = cs.MajorVerificationStatus.ToString(),
                MemberCode = cs.MemberCode,
                EnrollmentStatus = cs.EnrollmentStatus.ToString(),
                TeamId = activeTeamMember?.TeamId,
                TeamName = activeTeamMember?.Team?.TeamName,
                IsTeamLeader = activeTeamMember?.RoleInTeam == TeamMemberRole.Leader,
                JoinedAtUtc = cs.CreatedAt
            };
        }).ToList();

        var response = new ClassRosterListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };

        return Result.Success(response);
    }
}

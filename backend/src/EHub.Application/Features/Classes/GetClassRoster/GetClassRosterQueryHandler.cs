using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
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
        var targetClass = await _context.Classes
            .AsNoTracking()
            .Include(c => c.ClassLecturers)
            .FirstOrDefaultAsync(c => c.Id == classId, cancellationToken);

        if (targetClass == null)
        {
            return Result.Failure<ClassRosterListResponse>(
                new Error("Classes.NotFound", "The requested class was not found."));
        }

        var isAdmin = string.Equals(currentUserRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isLecturer = string.Equals(currentUserRole, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

        if (isLecturer)
        {
            var isAssigned = targetClass.PrimaryLecturerId == currentUserId ||
                             targetClass.ClassLecturers.Any(cl => cl.LecturerId == currentUserId);

            if (!isAssigned)
            {
                return Result.Failure<ClassRosterListResponse>(
                    new Error("Classes.AccessDenied", "You can only view roster for classes assigned to you."));
            }
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize is <= 0 or > 200 ? 20 : request.PageSize;

        var query = _context.ClassStudents
            .AsNoTracking()
            .Include(cs => cs.Student)
            .Include(cs => cs.TeamMembers)
            .ThenInclude(tm => tm.Team)
            .Where(cs => cs.ClassId == classId);

        // Search filter
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var searchTerm = request.Search.Trim().ToLower();
            query = query.Where(cs =>
                (cs.Student.RollNumber != null && cs.Student.RollNumber.ToLower().Contains(searchTerm)) ||
                (cs.Student.FullName != null && cs.Student.FullName.ToLower().Contains(searchTerm)) ||
                (cs.Student.Email != null && cs.Student.Email.ToLower().Contains(searchTerm)));
        }

        // Major filter
        if (!string.IsNullOrWhiteSpace(request.MajorCode))
        {
            var major = request.MajorCode.Trim().ToUpper();
            query = query.Where(cs => cs.Student.MajorCode != null && cs.Student.MajorCode.ToUpper() == major);
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (Enum.TryParse<EnrollmentStatus>(request.Status, true, out var statusEnum))
            {
                query = query.Where(cs => cs.EnrollmentStatus == statusEnum);
            }
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var classStudents = await query
            .OrderBy(cs => cs.Student.RollNumber)
            .ThenBy(cs => cs.Student.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = classStudents.Select(cs =>
        {
            var activeTeamMember = cs.TeamMembers.FirstOrDefault(tm => tm.Team != null && tm.Team.Status == TeamStatus.Active);
            return new ClassStudentDto
            {
                StudentId = cs.StudentId,
                RollNumber = cs.Student.RollNumber ?? string.Empty,
                FullName = cs.Student.FullName,
                Email = cs.Student.Email ?? string.Empty,
                MajorCode = cs.Student.MajorCode,
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

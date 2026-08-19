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
            .Where(cs => cs.ClassId == classId);

        query = ClassRosterFilters.Apply(query, request.Search, request.MajorCode, status);

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));

        var rosterRows = await query
            .OrderBy(cs => cs.Student.RollNumber)
            .ThenBy(cs => cs.Student.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(cs => new ClassRosterStudentProjection
            {
                StudentId = cs.StudentId,
                RollNumber = cs.Student.RollNumber ?? string.Empty,
                FullName = cs.Student.FullName,
                Email = cs.Student.Email ?? string.Empty,
                MajorCodeAtEnrollment = cs.MajorCodeAtEnrollment,
                ProfileMajorCode = cs.Student.MajorCode,
                MajorVerificationStatus = cs.MajorVerificationStatus.ToString(),
                MemberCode = cs.MemberCode,
                EnrollmentStatus = cs.EnrollmentStatus.ToString(),
                TeamId = cs.TeamMembers
                    .Where(tm => tm.CountsTowardActiveTeam && tm.Team.Status == TeamStatus.Active)
                    .Select(tm => (Guid?)tm.TeamId)
                    .FirstOrDefault(),
                TeamName = cs.TeamMembers
                    .Where(tm => tm.CountsTowardActiveTeam && tm.Team.Status == TeamStatus.Active)
                    .Select(tm => tm.Team.TeamName)
                    .FirstOrDefault(),
                IsTeamLeader = cs.TeamMembers.Any(tm =>
                    tm.CountsTowardActiveTeam &&
                    tm.Team.Status == TeamStatus.Active &&
                    tm.RoleInTeam == TeamMemberRole.Leader),
                JoinedAtUtc = cs.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Older class imports could create an unlinked Student row before the
        // student registered. Until those rows are naturally reconciled, use the
        // registered profile with the same normalized email as a safe major
        // fallback so existing rosters do not remain permanently "Missing".
        var registeredMajorByEmail = await RegisteredStudentMajorResolver.LoadByEmailAsync(
            _context,
            rosterRows.Select(row => row.Email),
            cancellationToken);

        var items = rosterRows
            .Select(row =>
            {
                var profileMajorCode = string.IsNullOrWhiteSpace(row.ProfileMajorCode)
                    ? null
                    : row.ProfileMajorCode.Trim().ToUpperInvariant();
                if (!MajorCodes.IsValid(profileMajorCode) &&
                    registeredMajorByEmail.TryGetValue(row.Email, out var registeredMajorCode))
                {
                    profileMajorCode = registeredMajorCode;
                }

                return new ClassStudentDto
                {
                    StudentId = row.StudentId,
                    RollNumber = row.RollNumber,
                    FullName = row.FullName,
                    Email = row.Email,
                    MajorCode = StudentEnrollmentRules.ResolveEffectiveMajorCode(
                        row.MajorCodeAtEnrollment,
                        profileMajorCode),
                    ProfileMajorCode = profileMajorCode,
                    MajorVerificationStatus = row.MajorVerificationStatus,
                    MemberCode = row.MemberCode,
                    EnrollmentStatus = row.EnrollmentStatus,
                    TeamId = row.TeamId,
                    TeamName = row.TeamName,
                    IsTeamLeader = row.IsTeamLeader,
                    JoinedAtUtc = row.JoinedAtUtc
                };
            })
            .ToList();

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

    private sealed class ClassRosterStudentProjection
    {
        public Guid StudentId { get; init; }
        public string RollNumber { get; init; } = string.Empty;
        public string FullName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? MajorCodeAtEnrollment { get; init; }
        public string? ProfileMajorCode { get; init; }
        public string MajorVerificationStatus { get; init; } = string.Empty;
        public string? MemberCode { get; init; }
        public string EnrollmentStatus { get; init; } = string.Empty;
        public Guid? TeamId { get; init; }
        public string? TeamName { get; init; }
        public bool IsTeamLeader { get; init; }
        public DateTime JoinedAtUtc { get; init; }
    }
}

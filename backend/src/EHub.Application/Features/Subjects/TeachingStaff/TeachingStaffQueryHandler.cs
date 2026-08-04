using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Subjects.TeachingStaff;

public sealed class TeachingStaffQueryHandler(IApplicationDbContext context) : ITeachingStaffQueryHandler
{
    public async Task<Result<TeachingStaffListResponse>> GetAsync(
        string semester,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseSemesterTerm(semester, out var term) || year is < 2000 or > 2100)
        {
            return Result.Failure<TeachingStaffListResponse>(
                new Error("VALIDATION_ERROR", "Semester and year are invalid."));
        }

        var staff = await context.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Where(user => user.UserRoles.Any(userRole =>
                userRole.Role.Name == SystemRoles.Lecturer ||
                userRole.Role.Name == SystemRoles.Mentor))
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var lecturerAssignments = await context.ClassLecturers
            .AsNoTracking()
            .Where(assignment =>
                assignment.Class.Semester.Term == term &&
                assignment.Class.Semester.Year == year)
            .Select(assignment => new StaffAssignment(
                assignment.LecturerId,
                assignment.ClassId,
                assignment.Class.ClassCode,
                assignment.Class.Course.Code))
            .ToListAsync(cancellationToken);

        var mentorAssignments = await context.MentorAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.Status == MentorAssignmentStatus.Active &&
                assignment.Team.Class.Semester.Term == term &&
                assignment.Team.Class.Semester.Year == year)
            .Select(assignment => new StaffAssignment(
                assignment.MentorProfile.UserId,
                assignment.Team.ClassId,
                assignment.Team.Class.ClassCode,
                assignment.Team.Class.Course.Code))
            .ToListAsync(cancellationToken);

        var assignments = lecturerAssignments
            .Concat(mentorAssignments)
            .GroupBy(item => new { item.UserId, item.ClassId })
            .Select(group => group.First())
            .ToLookup(item => item.UserId);

        var response = staff.Select(user =>
        {
            var role = user.UserRoles.Any(userRole => userRole.Role.Name == SystemRoles.Lecturer)
                ? "LECTURER"
                : "MENTOR";
            var memberAssignments = assignments[user.Id]
                .Select(item => new TeachingAssignmentResponse
                {
                    Id = $"{item.UserId:N}-{item.ClassId:N}",
                    ClassCode = item.ClassCode,
                    SubjectCode = item.SubjectCode,
                })
                .ToArray();

            return new TeachingStaffResponse
            {
                Id = user.Id,
                Name = user.FullName,
                Email = user.Email,
                Avatar = user.AvatarUrl,
                Role = role,
                Status = user.Status.ToString(),
                ClassCount = memberAssignments.Length,
                Assignments = memberAssignments,
            };
        }).ToArray();

        var distinctClasses = assignments
            .SelectMany(group => group)
            .Select(item => item.ClassId)
            .Distinct()
            .Count();

        return Result.Success(new TeachingStaffListResponse
        {
            Staff = response,
            Summary = new TeachingStaffSummaryResponse
            {
                Lecturers = response.Count(item => item.Role == "LECTURER"),
                Mentors = response.Count(item => item.Role == "MENTOR"),
                Assigned = response.Count(item => item.ClassCount > 0),
                Unassigned = response.Count(item => item.ClassCount == 0),
                Classes = distinctClasses,
            },
        });
    }

    private static bool TryParseSemesterTerm(string? value, out SemesterTerm result)
    {
        result = value?.Trim().ToUpperInvariant() switch
        {
            "SP" => SemesterTerm.Spring,
            "SU" => SemesterTerm.Summer,
            "FA" => SemesterTerm.Fall,
            _ => default,
        };

        return value is not null && result is SemesterTerm.Spring or SemesterTerm.Summer or SemesterTerm.Fall;
    }

    private sealed record StaffAssignment(Guid UserId, Guid ClassId, string ClassCode, string SubjectCode);
}

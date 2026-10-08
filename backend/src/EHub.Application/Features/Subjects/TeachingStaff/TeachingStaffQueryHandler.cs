using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
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
                new Error(ErrorCodes.ClassValidationError, "Semester and year are invalid."));
        }

        var targetSemester = await context.Semesters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Term == term && item.Year == year,
                cancellationToken);

        if (targetSemester == null)
        {
            return Result.Success(new TeachingStaffListResponse());
        }

        var staff = await context.SemesterStaffAssignments
            .AsNoTracking()
            .Include(item => item.User)
            .Where(item => item.SemesterId == targetSemester.Id)
            .OrderBy(item => item.User.FullName)
            .ThenBy(item => item.Role)
            .ToListAsync(cancellationToken);

        var incompleteMentors = await context.MentorImportDrafts
            .AsNoTracking()
            .Where(item =>
                item.SemesterId == targetSemester.Id &&
                item.Status == MentorImportDraftStatus.NeedsCompletion)
            .OrderBy(item => item.FullName)
            .ToListAsync(cancellationToken);

        var lecturerAssignments = await context.ClassLecturers
            .AsNoTracking()
            .Where(assignment => assignment.Class.SemesterId == targetSemester.Id)
            .Select(assignment => new StaffClassAssignment(
                assignment.LecturerId,
                assignment.ClassId,
                assignment.Class.ClassCode,
                assignment.Class.Course.Code,
                SemesterStaffRole.Lecturer))
            .ToListAsync(cancellationToken);

        var mentorAssignments = await context.MentorAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.Status == MentorAssignmentStatus.Active &&
                assignment.Team.Class.SemesterId == targetSemester.Id)
            .Select(assignment => new StaffClassAssignment(
                assignment.MentorProfile.UserId,
                assignment.Team.ClassId,
                assignment.Team.Class.ClassCode,
                assignment.Team.Class.Course.Code,
                SemesterStaffRole.Mentor))
            .ToListAsync(cancellationToken);

        var assignmentsByRole = lecturerAssignments
            .Concat(mentorAssignments)
            .GroupBy(item => new
            {
                item.UserId,
                item.Role,
                item.ClassId
            })
            .Select(group => group.First())
            .ToLookup(item => new
            {
                item.UserId,
                item.Role
            });

        var staffUserIds = staff.Select(item => item.UserId).Distinct().ToArray();
        var mentorTypes = await context.MentorProfiles
            .AsNoTracking()
            .Where(profile => staffUserIds.Contains(profile.UserId))
            .Select(profile => new { profile.UserId, profile.Type })
            .ToDictionaryAsync(profile => profile.UserId, profile => profile.Type, cancellationToken);

        var activeStaffResponse = staff.Select(item =>
        {
            var memberAssignments = assignmentsByRole[new
                {
                    item.UserId,
                    item.Role
                }]
                .Select(assignment => new TeachingAssignmentResponse
                {
                    Id = $"{assignment.UserId:N}-{assignment.ClassId:N}",
                    ClassCode = assignment.ClassCode,
                    SubjectCode = assignment.SubjectCode
                })
                .ToArray();

            return new TeachingStaffResponse
            {
                Id = item.Id,
                UserId = item.UserId,
                Name = item.User.FullName,
                Email = item.User.Email,
                Avatar = item.User.AvatarUrl,
                Role = ToRoleCode(item.Role),
                MentorType = item.Role == SemesterStaffRole.Mentor && mentorTypes.TryGetValue(item.UserId, out var mentorType)
                    ? mentorType.ToString()
                    : null,
                Status = item.Status.ToString(),
                UserStatus = item.User.Status.ToString(),
                IsIncomplete = false,
                ClassCount = memberAssignments.Length,
                Assignments = memberAssignments,
                RowVersion = item.Version.ToString()
            };
        });

        var incompleteMentorResponse = incompleteMentors.Select(item => new TeachingStaffResponse
        {
            Id = item.Id,
            UserId = null,
            Name = item.FullName,
            Email = item.Email ?? string.Empty,
            Role = "MENTOR",
            MentorType = item.Type.ToString(),
            Status = "Incomplete",
            UserStatus = "NotCreated",
            IsIncomplete = true,
            MissingFields = GetMissingFields(item),
            ClassCount = 0,
            Assignments = Array.Empty<TeachingAssignmentResponse>(),
            RowVersion = string.Empty
        });

        var response = activeStaffResponse
            .Concat(incompleteMentorResponse)
            .OrderBy(item => item.Name)
            .ThenBy(item => item.Role)
            .ToArray();

        var distinctClasses = lecturerAssignments
            .Concat(mentorAssignments)
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
                Classes = distinctClasses
            }
        });
    }

    public async Task<Result<TeachingStaffCandidateListResponse>> GetCandidatesAsync(
        CancellationToken cancellationToken = default)
    {
        var users = await context.Users
            .AsNoTracking()
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Include(user => user.MentorProfile)
            .Where(user =>
                user.Status == UserStatus.Active &&
                user.UserRoles.Any(userRole =>
                    userRole.Role.Name == SystemRoles.Lecturer ||
                    userRole.Role.Name == SystemRoles.Mentor))
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var candidates = users
            .SelectMany(user => user.UserRoles
                .Where(userRole =>
                    userRole.Role.Name == SystemRoles.Lecturer ||
                    userRole.Role.Name == SystemRoles.Mentor)
                .Select(userRole => new TeachingStaffCandidateResponse
                {
                    UserId = user.Id,
                    Name = user.FullName,
                    Email = user.Email,
                    Avatar = user.AvatarUrl,
                    Role = string.Equals(
                        userRole.Role.Name,
                        SystemRoles.Lecturer,
                        StringComparison.OrdinalIgnoreCase)
                        ? "LECTURER"
                        : "MENTOR",
                    MentorType = string.Equals(userRole.Role.Name, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase)
                        ? user.MentorProfile?.Type.ToString()
                        : null,
                    ContractType = string.Equals(userRole.Role.Name, SystemRoles.Mentor, StringComparison.OrdinalIgnoreCase)
                        ? user.MentorProfile?.ContractType
                        : null
                }))
            .ToArray();

        return Result.Success(new TeachingStaffCandidateListResponse
        {
            Candidates = candidates
        });
    }

    private static bool TryParseSemesterTerm(string? value, out SemesterTerm result)
    {
        var normalized = value?.Trim().ToUpperInvariant();
        result = normalized switch
        {
            "SP" => SemesterTerm.Spring,
            "SU" => SemesterTerm.Summer,
            "FA" => SemesterTerm.Fall,
            _ => default
        };

        return normalized is "SP" or "SU" or "FA";
    }

    private static string ToRoleCode(SemesterStaffRole role) => role switch
    {
        SemesterStaffRole.Lecturer => "LECTURER",
        SemesterStaffRole.Mentor => "MENTOR",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    private static IReadOnlyCollection<string> GetMissingFields(MentorImportDraft mentor)
    {
        var fields = new List<string>();
        if (string.IsNullOrWhiteSpace(mentor.Email))
        {
            fields.Add(mentor.Type == MentorType.Academic ? "Email công việc" : "Email");
        }

        if (mentor.Type == MentorType.Enterprise)
        {
            if (mentor.DateOfBirth is null) fields.Add("Ngày tháng năm sinh");
            if (string.IsNullOrWhiteSpace(mentor.Phone)) fields.Add("SDT");
            if (string.IsNullOrWhiteSpace(mentor.ContractType)) fields.Add("Loại HĐ");
            if (string.IsNullOrWhiteSpace(mentor.EducationLevel)) fields.Add("Trình độ học vấn");
            if (string.IsNullOrWhiteSpace(mentor.CurrentAddress)) fields.Add("Địa chỉ hiện nay");
            if (string.IsNullOrWhiteSpace(mentor.JobTitle)) fields.Add("Vị trí, Chức danh");
            if (string.IsNullOrWhiteSpace(mentor.Organization)) fields.Add("Công ty");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(mentor.Department)) fields.Add("Phòng ban trực tiếp");
            if (string.IsNullOrWhiteSpace(mentor.JobTitle)) fields.Add("Chức danh (VN)");
        }

        return fields;
    }

    private sealed record StaffClassAssignment(
        Guid UserId,
        Guid ClassId,
        string ClassCode,
        string SubjectCode,
        SemesterStaffRole Role);
}

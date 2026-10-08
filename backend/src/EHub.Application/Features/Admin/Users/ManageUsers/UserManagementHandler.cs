using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Users;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Admin.Users.ManageUsers;

public sealed class UserManagementHandler(IApplicationDbContext context, ICurrentUserService currentUser, IPasswordHasher passwordHasher) : IUserManagementHandler
{
    private static readonly IReadOnlyDictionary<string, string[]> ValidMajors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["BBA"] = [MajorCodes.BBA_HM, MajorCodes.BBA_FIN, MajorCodes.BBA_IB, MajorCodes.BBA_MC, MajorCodes.BBA_MKT, MajorCodes.BBA_TM],
        ["BEN"] = [MajorCodes.BEN],
        ["BIT"] = [MajorCodes.BIT_AI, MajorCodes.BIT_GD, MajorCodes.BIT_IA, MajorCodes.BIT_SE]
    };
    public async Task<Result<ManagedUserListResponse>> GetUsersAsync(int page, int limit, string? search, string? role, string? status, string? mentorType, CancellationToken token = default)
    {
        if (!CanReadDirectory) return Fail<ManagedUserListResponse>(ErrorCodes.CommonForbiddenError, "Staff access is required.");
        if (page < 1 || limit is < 1 or > 100) return Fail<ManagedUserListResponse>("VALIDATION_ERROR", "Page and limit are invalid.");
        var query = ReadUsersQuery();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(user => user.FullName.ToLower().Contains(term) || user.Email.ToLower().Contains(term) || (user.Student != null && user.Student.RollNumber != null && user.Student.RollNumber.ToLower().Contains(term))); }
        var roleName = string.Empty; if (!string.IsNullOrWhiteSpace(role) && !TryRole(role, out roleName)) return Fail<ManagedUserListResponse>("VALIDATION_ERROR", "Role is invalid."); if (!string.IsNullOrWhiteSpace(role)) query = query.Where(user => user.UserRoles.Any(item => item.Role.Name == roleName));
        var userStatus = UserStatus.Active; if (!string.IsNullOrWhiteSpace(status) && !TryStatus(status, out userStatus)) return Fail<ManagedUserListResponse>("VALIDATION_ERROR", "Status is invalid."); if (!string.IsNullOrWhiteSpace(status)) query = query.Where(user => user.Status == userStatus);
        if (!string.IsNullOrWhiteSpace(mentorType))
        {
            if (!Enum.TryParse<MentorType>(mentorType.Trim(), ignoreCase: true, out var type) || !Enum.IsDefined(type))
                return Fail<ManagedUserListResponse>("VALIDATION_ERROR", "Mentor type is invalid.");
            query = query.Where(user => user.MentorProfile != null && user.MentorProfile.Type == type);
        }
        var total = await query.CountAsync(token);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)limit));
        var users = await query
            .OrderByDescending(user => user.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(token);
        var academicContexts = await GetCurrentSemesterContextsAsync(users.Select(user => user.Id).ToArray(), token);

        return Result.Success(new ManagedUserListResponse
        {
            Users = users.Select(user => ToResponse(
                user,
                academicContext: academicContexts.GetValueOrDefault(user.Id))).ToArray(),
            Pagination = new PaginationResponse { Total = total, Page = page, Limit = limit, Pages = pages }
        });
    }
    public async Task<Result<ManagedUserResponse>> GetUserAsync(Guid id, CancellationToken token = default)
    {
        if (!CanReadDirectory) return Fail<ManagedUserResponse>(ErrorCodes.CommonForbiddenError, "Staff access is required.");
        var user = await ReadUsersQuery().FirstOrDefaultAsync(item => item.Id == id, token);
        if (user is null) return IsAdminReader
            ? Fail<ManagedUserResponse>("NOT_FOUND", "User was not found.")
            : Fail<ManagedUserResponse>(ErrorCodes.CommonForbiddenError, "The user is unavailable or outside your class scope.");
        return Result.Success(ToResponse(user));
    }

    private bool IsAdminReader => currentUser.Roles.Contains(SystemRoles.Admin, StringComparer.OrdinalIgnoreCase);
    private bool CanReadDirectory => IsAdminReader || currentUser.Roles.Contains(SystemRoles.Lecturer, StringComparer.OrdinalIgnoreCase);

    private IQueryable<User> ReadUsersQuery()
    {
        var query = context.Users.AsNoTracking().Include(user => user.UserRoles).ThenInclude(item => item.Role)
            .Include(user => user.Student).Include(user => user.MentorProfile).AsQueryable();
        if (IsAdminReader) return query;
        var lecturerId = currentUser.UserId;
        // Staff directory entries stay available; student profiles require an
        // active/completed enrollment in one of this lecturer's assigned classes.
        return query.Where(user => user.Id == lecturerId ||
            user.Student == null && !user.UserRoles.Any(item => item.Role.Name == SystemRoles.Student) ||
            user.Student != null && user.Student.ClassStudents.Any(enrollment =>
                enrollment.EnrollmentStatus != EnrollmentStatus.Dropped &&
                (enrollment.Class.PrimaryLecturerId == lecturerId ||
                 enrollment.Class.ClassLecturers.Any(assignment => assignment.LecturerId == lecturerId))));
    }
    public async Task<Result<ManagedUserResponse>> CreateUserAsync(SaveManagedUserRequest request, CancellationToken token = default)
    {
        var validation = await ValidateAsync(request, null, true, token); if (validation is not null) return Fail<ManagedUserResponse>("VALIDATION_ERROR", validation);
        var mentorTypeError = await ValidateMentorTypeAsync(request, null, token); if (mentorTypeError is not null) return Fail<ManagedUserResponse>("VALIDATION_ERROR", mentorTypeError);
        var role = await context.Roles.FirstAsync(item => item.Name == NormalizeRole(request.Role), token); var email = request.Email.Trim().ToLowerInvariant(); var user = new User { FullName = request.Name.Trim(), Email = email, NormalizedEmail = email, PasswordHash = passwordHasher.Hash(request.Password!), Phone = Clean(request.Phone), Status = ToStatus(request.Status), IsEmailVerified = true, CreatedBy = currentUser.UserId };
        var requestedRole = NormalizeRole(request.Role);
        await context.Users.AddAsync(user, token);
        await context.UserRoles.AddAsync(new UserRole
        {
            UserId = user.Id,
            RoleId = role.Id,
            User = user,
            Role = role,
            AssignedBy = currentUser.UserId
        }, token);
        if (requestedRole == SystemRoles.Student) await context.Students.AddAsync(NewStudent(user, request), token);
        if (requestedRole == SystemRoles.Mentor) await context.MentorProfiles.AddAsync(NewMentorProfile(user, ParseMentorType(request.MentorType)!.Value), token);
        await context.SaveChangesAsync(token);
        return Result.Success(ToResponse(user, request));
    }
    public async Task<Result<ManagedUserResponse>> UpdateUserAsync(Guid id, SaveManagedUserRequest request, CancellationToken token = default)
    {
        var user = await GetUserEntityAsync(id, token);
        if (user is null) return Fail<ManagedUserResponse>("NOT_FOUND", "User was not found.");
        var validation = await ValidateAsync(request, user, false, token);
        if (validation is not null) return Fail<ManagedUserResponse>("VALIDATION_ERROR", validation);
        var mentorTypeError = await ValidateMentorTypeAsync(request, user, token);
        if (mentorTypeError is not null) return Fail<ManagedUserResponse>("VALIDATION_ERROR", mentorTypeError);

        var requestedRole = NormalizeRole(request.Role);
        if (id == currentUser.UserId &&
            (requestedRole != user.UserRoles.First().Role.Name || ToStatus(request.Status) != user.Status))
        {
            return Fail<ManagedUserResponse>("BUSINESS_RULE", "You cannot change your own role or status.");
        }
        if (user.UserRoles.Any(item => item.Role.Name == SystemRoles.Admin) &&
            requestedRole != SystemRoles.Admin &&
            await AdminCount(token) <= 1)
        {
            return Fail<ManagedUserResponse>("BUSINESS_RULE", "The last admin cannot be demoted.");
        }

        user.FullName = request.Name.Trim();
        user.Email = request.Email.Trim().ToLowerInvariant();
        user.NormalizedEmail = user.Email;
        user.Phone = Clean(request.Phone);
        user.Status = ToStatus(request.Status);

        var currentRole = user.UserRoles.First();
        if (currentRole.Role.Name != requestedRole)
        {
            var role = await context.Roles.FirstAsync(item => item.Name == requestedRole, token);
            context.UserRoles.Remove(currentRole);
            user.UserRoles.Remove(currentRole);

            var replacement = new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                User = user,
                Role = role,
                AssignedBy = currentUser.UserId
            };
            await context.UserRoles.AddAsync(replacement, token);
            user.UserRoles.Add(replacement);
        }

        if (requestedRole == SystemRoles.Student) { var student = user.Student ?? await context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.UserId == user.Id, token); if (student is null) await context.Students.AddAsync(NewStudent(user, request), token); else UpdateStudent(student, request, user); } else if (user.Student is not null) { user.Student.IsDeleted = true; user.Student.DeletedAt = DateTime.UtcNow; }
        if (requestedRole == SystemRoles.Mentor && !await context.MentorProfiles.AnyAsync(item => item.UserId == user.Id, token)) await context.MentorProfiles.AddAsync(NewMentorProfile(user, ParseMentorType(request.MentorType)!.Value), token);
        await context.SaveChangesAsync(token); return Result.Success(ToResponse(user, request));
    }
    public async Task<Result> DeleteUserAsync(Guid id, CancellationToken token = default)
    {
        if (id == currentUser.UserId) return Fail("BUSINESS_RULE", "You cannot delete your own account."); var user = await GetUserEntityAsync(id, token); if (user is null) return Fail("NOT_FOUND", "User was not found."); if (user.UserRoles.Any(item => item.Role.Name == SystemRoles.Admin) && await AdminCount(token) <= 1) return Fail("BUSINESS_RULE", "The last admin cannot be deleted."); if (user.Student is not null || await context.Evaluations.AnyAsync(item => item.EvaluatorId == id, token) || await context.Classes.AnyAsync(item => item.CreatedById == id, token) || await context.Projects.AnyAsync(item => item.CreatedById == id, token)) return Fail("RELATED_DATA_EXISTS", "This account has important related data and cannot be deleted."); context.Users.Remove(user); await context.SaveChangesAsync(token); return Result.Success();
    }
    private async Task<IReadOnlyDictionary<Guid, UserAcademicContext>> GetCurrentSemesterContextsAsync(
        Guid[] userIds,
        CancellationToken token)
    {
        var result = new Dictionary<Guid, UserAcademicContext>();
        if (userIds.Length == 0) return result;

        var currentSemester = await context.Semesters
            .AsNoTracking()
            .Where(semester => semester.Status == SemesterStatus.Active)
            .OrderByDescending(semester => semester.Year)
            .ThenByDescending(semester => semester.Term)
            .Select(semester => new { semester.Id, semester.Code })
            .FirstOrDefaultAsync(token);
        if (currentSemester is null) return result;

        var semesterStaff = await context.SemesterStaffAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.SemesterId == currentSemester.Id &&
                assignment.Status == SemesterStaffStatus.Active &&
                userIds.Contains(assignment.UserId))
            .Select(assignment => assignment.UserId)
            .Distinct()
            .ToListAsync(token);
        foreach (var userId in semesterStaff)
        {
            GetOrCreateAcademicContext(result, userId, currentSemester.Code);
        }

        var studentClasses = await context.ClassStudents
            .AsNoTracking()
            .Where(enrollment =>
                enrollment.SemesterId == currentSemester.Id &&
                enrollment.EnrollmentStatus != EnrollmentStatus.Dropped &&
                (IsAdminReader || enrollment.Class.PrimaryLecturerId == currentUser.UserId ||
                 enrollment.Class.ClassLecturers.Any(assignment => assignment.LecturerId == currentUser.UserId)) &&
                enrollment.Student.UserId.HasValue &&
                userIds.Contains(enrollment.Student.UserId.Value))
            .Select(enrollment => new
            {
                UserId = enrollment.Student.UserId!.Value,
                enrollment.Class.ClassCode
            })
            .ToListAsync(token);
        foreach (var item in studentClasses)
        {
            GetOrCreateAcademicContext(result, item.UserId, currentSemester.Code).Classes.Add(item.ClassCode);
        }

        var studentTeams = await context.TeamMembers
            .AsNoTracking()
            .Where(member =>
                member.Team.Class.SemesterId == currentSemester.Id &&
                member.Team.Status == TeamStatus.Active &&
                member.CountsTowardActiveTeam &&
                member.ClassStudent.EnrollmentStatus != EnrollmentStatus.Dropped &&
                (IsAdminReader || member.Team.Class.PrimaryLecturerId == currentUser.UserId ||
                 member.Team.Class.ClassLecturers.Any(assignment => assignment.LecturerId == currentUser.UserId)) &&
                member.ClassStudent.Student.UserId.HasValue &&
                userIds.Contains(member.ClassStudent.Student.UserId.Value))
            .Select(member => new
            {
                UserId = member.ClassStudent.Student.UserId!.Value,
                member.Team.TeamName
            })
            .ToListAsync(token);
        foreach (var item in studentTeams)
        {
            GetOrCreateAcademicContext(result, item.UserId, currentSemester.Code).GroupNames.Add(item.TeamName);
        }

        var lecturerClasses = await context.ClassLecturers
            .AsNoTracking()
            .Where(assignment =>
                assignment.Class.SemesterId == currentSemester.Id &&
                assignment.Class.Status != ClassStatus.Archived &&
                userIds.Contains(assignment.LecturerId))
            .Select(assignment => new { UserId = assignment.LecturerId, assignment.Class.ClassCode })
            .ToListAsync(token);
        foreach (var item in lecturerClasses)
        {
            GetOrCreateAcademicContext(result, item.UserId, currentSemester.Code).Classes.Add(item.ClassCode);
        }

        var mentorTeams = await context.MentorAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.Team.Class.SemesterId == currentSemester.Id &&
                assignment.Team.Status == TeamStatus.Active &&
                assignment.Status == MentorAssignmentStatus.Active &&
                userIds.Contains(assignment.MentorProfile.UserId))
            .Select(assignment => new
            {
                UserId = assignment.MentorProfile.UserId,
                assignment.Team.Class.ClassCode,
                assignment.Team.TeamName
            })
            .ToListAsync(token);
        foreach (var item in mentorTeams)
        {
            var academicContext = GetOrCreateAcademicContext(result, item.UserId, currentSemester.Code);
            academicContext.Classes.Add(item.ClassCode);
            academicContext.GroupNames.Add(item.TeamName);
        }

        return result;
    }

    private static UserAcademicContext GetOrCreateAcademicContext(
        IDictionary<Guid, UserAcademicContext> contexts,
        Guid userId,
        string semester)
    {
        if (!contexts.TryGetValue(userId, out var academicContext))
        {
            academicContext = new UserAcademicContext(semester);
            contexts[userId] = academicContext;
        }

        return academicContext;
    }

    private async Task<string?> ValidateAsync(SaveManagedUserRequest request, User? existing, bool creating, CancellationToken token) { if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _)) return "Name and a valid email are required."; if (creating && (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)) return "Temporary password must contain at least 6 characters."; if (!TryRole(request.Role, out _) || !TryStatus(request.Status, out _)) return "Role or status is invalid."; var email = request.Email.Trim().ToLowerInvariant(); if (await context.Users.AnyAsync(item => item.NormalizedEmail == email && (existing == null || item.Id != existing.Id), token)) return "Email already exists."; if (NormalizeRole(request.Role) == SystemRoles.Student) { if (string.IsNullOrWhiteSpace(request.StudentId) || string.IsNullOrWhiteSpace(request.ProgramGroup) || string.IsNullOrWhiteSpace(request.Major)) return "Student ID, program group, and major are required for students."; if (!ValidMajors.TryGetValue(request.ProgramGroup.Trim(), out var majors) || !majors.Contains(request.Major.Trim().ToUpperInvariant())) return "Major is invalid for the selected program group."; var existingStudentId = existing?.Student?.Id ?? Guid.Empty; if (await context.Students.AnyAsync(item => item.NormalizedRollNumber == request.StudentId.Trim().ToUpperInvariant() && item.Id != existingStudentId, token)) return "Student ID already exists."; } return null; }
    private static MentorType? ParseMentorType(string? value) =>
        Enum.TryParse<MentorType>(value?.Trim(), ignoreCase: true, out var type) && Enum.IsDefined(type) ? type : null;

    // A new mentor must say which kind they are, because that decides the team slot they can fill. The kind of an
    // existing mentor is not changed here: moving a mentor between kinds would leave their current assignments invalid.
    private async Task<string?> ValidateMentorTypeAsync(SaveManagedUserRequest request, User? existing, CancellationToken token)
    {
        if (NormalizeRole(request.Role) != SystemRoles.Mentor) return null;
        var requested = ParseMentorType(request.MentorType);
        var profile = existing is null ? null : await context.MentorProfiles.AsNoTracking().FirstOrDefaultAsync(item => item.UserId == existing.Id, token);
        if (profile is null) return requested is null ? "Mentor type must be Enterprise or Academic." : null;
        if (!string.IsNullOrWhiteSpace(request.MentorType) && requested != profile.Type) return "The mentor type of an existing mentor cannot be changed here.";
        return null;
    }
    private Task<User?> GetUserEntityAsync(Guid id, CancellationToken token) => context.Users.Include(user => user.UserRoles).ThenInclude(item => item.Role).Include(user => user.Student).FirstOrDefaultAsync(user => user.Id == id, token);
    private Task<int> AdminCount(CancellationToken token) => context.UserRoles.CountAsync(item => item.Role.Name == SystemRoles.Admin, token);
    private static bool TryRole(string value, out string role) { role = NormalizeRole(value); return SystemRoles.All.Contains(role); } private static string NormalizeRole(string value) => value.Trim().ToLowerInvariant() switch { "admin" => SystemRoles.Admin, "lecturer" => SystemRoles.Lecturer, "mentor" => SystemRoles.Mentor, "student" => SystemRoles.Student, _ => string.Empty }; private static bool TryStatus(string value, out UserStatus status) { status = ToStatus(value); return value.Equals("PENDING", StringComparison.OrdinalIgnoreCase) || value.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) || value.Equals("REJECTED", StringComparison.OrdinalIgnoreCase); } private static UserStatus ToStatus(string value) => value.Trim().ToUpperInvariant() switch { "PENDING" => UserStatus.PendingApproval, "REJECTED" => UserStatus.Rejected, _ => UserStatus.Active }; private static string ToStatus(UserStatus value) => value == UserStatus.PendingApproval ? "PENDING" : value == UserStatus.Rejected ? "REJECTED" : "APPROVED"; private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Student NewStudent(User user, SaveManagedUserRequest request) => new() { UserId = user.Id, FullName = user.FullName, Email = user.Email, RollNumber = request.StudentId!.Trim(), NormalizedRollNumber = request.StudentId.Trim().ToUpperInvariant(), MajorCode = request.Major!.Trim().ToUpperInvariant(), AvatarUrl = user.AvatarUrl, ProgramGroup = ProgramGroup.Standard }; private static MentorProfile NewMentorProfile(User user, MentorType type) => new() { UserId = user.Id, User = user, Type = type, Status = MentorProfileStatus.Active }; private static void UpdateStudent(Student student, SaveManagedUserRequest request, User user) { student.IsDeleted = false; student.FullName = user.FullName; student.Email = user.Email; student.RollNumber = request.StudentId!.Trim(); student.NormalizedRollNumber = request.StudentId.Trim().ToUpperInvariant(); student.MajorCode = request.Major!.Trim().ToUpperInvariant(); }
    private static ManagedUserResponse ToResponse(
        User user,
        SaveManagedUserRequest? request = null,
        UserAcademicContext? academicContext = null) => new()
        {
            Id = user.Id,
            Name = user.FullName,
            Email = user.Email,
            Avatar = user.AvatarUrl,
            Role = user.UserRoles.FirstOrDefault()?.Role.Name.ToUpperInvariant() ?? "STUDENT",
            MentorType = user.MentorProfile?.Type.ToString(),
            Status = ToStatus(user.Status),
            StudentId = user.Student?.RollNumber ?? request?.StudentId,
            ProgramGroup = request?.ProgramGroup,
            Major = user.Student?.MajorCode ?? request?.Major,
            Phone = user.Phone,
            Semester = academicContext?.Semester,
            Class = JoinValues(academicContext?.Classes),
            GroupName = JoinValues(academicContext?.GroupNames),
            CreatedAt = user.CreatedAt
        };
    private static string? JoinValues(IEnumerable<string>? values) => values is null
        ? null
        : string.Join(", ", values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)) is { Length: > 0 } joined
            ? joined
            : null;
    private sealed class UserAcademicContext(string semester)
    {
        public string Semester { get; } = semester;
        public HashSet<string> Classes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> GroupNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
    private static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(new Error(code, message)); private static Result Fail(string code, string message) => Result.Failure(new Error(code, message));
}

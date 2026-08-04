using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Common;
using EHub.Contracts.Users;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class UsersController(IApplicationDbContext context, ICurrentUserService currentUser, IPasswordHasher passwordHasher) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, string[]> ValidMajors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["BIT"] = ["BIT_SE", "BIT_IA", "BIT_GD", "BIT_AI", "BIT_IS", "BIT_CS", "BIT_CY", "BIT_DS"],
        ["BBA"] = ["BBA_IB", "BBA_MKT", "BBA_HM", "BBA_MC", "BBA_TM", "BBA_FIN", "BBA_HRM", "BBA_DM", "BBA_BA", "BBA_LOG"],
        ["BLA"] = ["BLA_ELT", "BLA_BC", "BLA_JP", "BLA_KR", "BLA_CN"],
    };

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int limit = 10, [FromQuery] string? search = null, [FromQuery] string? role = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default)
    {
        if (page < 1 || limit is < 1 or > 100) return BadRequest(ApiResponse<object>.FailureResponse("Page and limit are invalid.", "VALIDATION_ERROR"));
        var query = context.Users.AsNoTracking().Include(user => user.UserRoles).ThenInclude(item => item.Role).Include(user => user.Student).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToLower(); query = query.Where(user => user.FullName.ToLower().Contains(term) || user.Email.ToLower().Contains(term) || (user.Student != null && user.Student.RollNumber != null && user.Student.RollNumber.ToLower().Contains(term))); }
        var roleName = string.Empty;
        if (!string.IsNullOrWhiteSpace(role) && !TryRole(role, out roleName)) return BadRequest(ApiResponse<object>.FailureResponse("Role is invalid.", "VALIDATION_ERROR"));
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(user => user.UserRoles.Any(item => item.Role.Name == roleName));
        var userStatus = UserStatus.Active;
        if (!string.IsNullOrWhiteSpace(status) && !TryStatus(status, out userStatus)) return BadRequest(ApiResponse<object>.FailureResponse("Status is invalid.", "VALIDATION_ERROR"));
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(user => user.Status == userStatus);
        var total = await query.CountAsync(cancellationToken); var pages = Math.Max(1, (int)Math.Ceiling(total / (double)limit));
        var users = await query.OrderByDescending(user => user.CreatedAt).Skip((page - 1) * limit).Take(limit).ToListAsync(cancellationToken);
        return Ok(ApiResponse<ManagedUserListResponse>.SuccessResponse(new ManagedUserListResponse { Users = users.Select(user => ToResponse(user)).ToArray(), Pagination = new PaginationResponse { Total = total, Page = page, Limit = limit, Pages = pages } }, "Users retrieved successfully."));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id, CancellationToken cancellationToken) => await GetUserEntity(id, cancellationToken) is { } user ? Ok(ApiResponse<ManagedUserResponse>.SuccessResponse(ToResponse(user), "User retrieved successfully.")) : NotFound(ApiResponse<object>.FailureResponse("User was not found.", "NOT_FOUND"));

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] SaveManagedUserRequest request, CancellationToken cancellationToken)
    {
        var error = await ValidateRequest(request, null, true, cancellationToken); if (error is not null) return BadRequest(ApiResponse<object>.FailureResponse(error, "VALIDATION_ERROR"));
        var role = await context.Roles.FirstAsync(item => item.Name == NormalizeRole(request.Role), cancellationToken);
        var user = new User { FullName = request.Name.Trim(), Email = request.Email.Trim().ToLowerInvariant(), NormalizedEmail = request.Email.Trim().ToLowerInvariant(), PasswordHash = passwordHasher.Hash(request.Password!), Phone = Clean(request.Phone), Status = ToStatus(request.Status), IsEmailVerified = true, CreatedBy = currentUser.UserId };
        await context.Users.AddAsync(user, cancellationToken); await context.UserRoles.AddAsync(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedBy = currentUser.UserId }, cancellationToken);
        if (NormalizeRole(request.Role) == SystemRoles.Student) await context.Students.AddAsync(NewStudent(user, request), cancellationToken);
        await context.SaveChangesAsync(cancellationToken); return CreatedAtAction(nameof(GetUser), new { id = user.Id }, ApiResponse<ManagedUserResponse>.SuccessResponse(ToResponse(user, request), "User created successfully."));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] SaveManagedUserRequest request, CancellationToken cancellationToken)
    {
        var user = await GetUserEntity(id, cancellationToken); if (user is null) return NotFound(ApiResponse<object>.FailureResponse("User was not found.", "NOT_FOUND"));
        var error = await ValidateRequest(request, user, false, cancellationToken); if (error is not null) return BadRequest(ApiResponse<object>.FailureResponse(error, "VALIDATION_ERROR"));
        var requestedRole = NormalizeRole(request.Role); if (id == currentUser.UserId && (requestedRole != user.UserRoles.First().Role.Name || ToStatus(request.Status) != user.Status)) return BadRequest(ApiResponse<object>.FailureResponse("You cannot change your own role or status.", "BUSINESS_RULE"));
        if (user.UserRoles.Any(item => item.Role.Name == SystemRoles.Admin) && requestedRole != SystemRoles.Admin && await AdminCount(cancellationToken) <= 1) return BadRequest(ApiResponse<object>.FailureResponse("The last admin cannot be demoted.", "BUSINESS_RULE"));
        user.FullName = request.Name.Trim(); user.Email = request.Email.Trim().ToLowerInvariant(); user.NormalizedEmail = user.Email; user.Phone = Clean(request.Phone); user.Status = ToStatus(request.Status);
        var roleLink = user.UserRoles.First(); if (roleLink.Role.Name != requestedRole) roleLink.RoleId = (await context.Roles.FirstAsync(item => item.Name == requestedRole, cancellationToken)).Id;
        if (requestedRole == SystemRoles.Student)
        {
            var student = user.Student ?? await context.Students.IgnoreQueryFilters().FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken);
            if (student is null) await context.Students.AddAsync(NewStudent(user, request), cancellationToken);
            else UpdateStudent(student, request, user);
        }
        else if (user.Student is not null) { user.Student.IsDeleted = true; user.Student.DeletedAt = DateTime.UtcNow; }
        await context.SaveChangesAsync(cancellationToken); return Ok(ApiResponse<ManagedUserResponse>.SuccessResponse(ToResponse(user, request), "User updated successfully."));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        if (id == currentUser.UserId) return BadRequest(ApiResponse<object>.FailureResponse("You cannot delete your own account.", "BUSINESS_RULE"));
        var user = await GetUserEntity(id, cancellationToken); if (user is null) return NotFound(ApiResponse<object>.FailureResponse("User was not found.", "NOT_FOUND"));
        if (user.UserRoles.Any(item => item.Role.Name == SystemRoles.Admin) && await AdminCount(cancellationToken) <= 1) return BadRequest(ApiResponse<object>.FailureResponse("The last admin cannot be deleted.", "BUSINESS_RULE"));
        if (user.Student is not null || await context.Evaluations.AnyAsync(item => item.EvaluatorId == id, cancellationToken) || await context.Classes.AnyAsync(item => item.CreatedById == id, cancellationToken) || await context.Projects.AnyAsync(item => item.CreatedById == id, cancellationToken)) return Conflict(ApiResponse<object>.FailureResponse("This account has important related data and cannot be deleted.", "RELATED_DATA_EXISTS"));
        context.Users.Remove(user); await context.SaveChangesAsync(cancellationToken); return Ok(ApiResponse<object?>.SuccessResponse(null, "User deleted successfully."));
    }

    private async Task<string?> ValidateRequest(SaveManagedUserRequest request, User? existing, bool creating, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email) || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _)) return "Name and a valid email are required.";
        if (creating && (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)) return "Temporary password must contain at least 6 characters.";
        if (!TryRole(request.Role, out _) || !TryStatus(request.Status, out _)) return "Role or status is invalid.";
        var email = request.Email.Trim().ToLowerInvariant(); if (await context.Users.AnyAsync(user => user.NormalizedEmail == email && (existing == null || user.Id != existing.Id), token)) return "Email already exists.";
        if (NormalizeRole(request.Role) == SystemRoles.Student) { if (string.IsNullOrWhiteSpace(request.StudentId) || string.IsNullOrWhiteSpace(request.ProgramGroup) || string.IsNullOrWhiteSpace(request.Major)) return "Student ID, program group, and major are required for students."; if (!ValidMajors.TryGetValue(request.ProgramGroup.Trim(), out var majors) || !majors.Contains(request.Major.Trim().ToUpperInvariant())) return "Major is invalid for the selected program group."; var existingStudentId = existing is null ? Guid.Empty : existing.Student?.Id ?? Guid.Empty; if (await context.Students.AnyAsync(student => student.NormalizedRollNumber == request.StudentId.Trim().ToUpperInvariant() && student.Id != existingStudentId, token)) return "Student ID already exists."; }
        return null;
    }
    private async Task<User?> GetUserEntity(Guid id, CancellationToken token) => await context.Users.Include(user => user.UserRoles).ThenInclude(item => item.Role).Include(user => user.Student).FirstOrDefaultAsync(user => user.Id == id, token);
    private async Task<int> AdminCount(CancellationToken token) => await context.UserRoles.CountAsync(item => item.Role.Name == SystemRoles.Admin, token);
    private static bool TryRole(string value, out string role) { role = NormalizeRole(value); return SystemRoles.All.Contains(role); }
    private static string NormalizeRole(string value) => value.Trim().ToLowerInvariant() switch { "admin" => SystemRoles.Admin, "lecturer" => SystemRoles.Lecturer, "mentor" => SystemRoles.Mentor, "student" => SystemRoles.Student, _ => string.Empty };
    private static bool TryStatus(string value, out UserStatus status) { status = ToStatus(value); return value.Equals("PENDING", StringComparison.OrdinalIgnoreCase) || value.Equals("APPROVED", StringComparison.OrdinalIgnoreCase) || value.Equals("REJECTED", StringComparison.OrdinalIgnoreCase); }
    private static UserStatus ToStatus(string value) => value.Trim().ToUpperInvariant() switch { "PENDING" => UserStatus.PendingApproval, "REJECTED" => UserStatus.Rejected, _ => UserStatus.Active };
    private static string ToStatus(UserStatus value) => value == UserStatus.PendingApproval ? "PENDING" : value == UserStatus.Rejected ? "REJECTED" : "APPROVED";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static Student NewStudent(User user, SaveManagedUserRequest request) => new() { UserId = user.Id, FullName = user.FullName, Email = user.Email, RollNumber = request.StudentId!.Trim(), NormalizedRollNumber = request.StudentId.Trim().ToUpperInvariant(), MajorCode = request.Major!.Trim().ToUpperInvariant(), AvatarUrl = user.AvatarUrl, ProgramGroup = ProgramGroup.Standard };
    private static void UpdateStudent(Student student, SaveManagedUserRequest request, User user) { student.IsDeleted = false; student.FullName = user.FullName; student.Email = user.Email; student.RollNumber = request.StudentId!.Trim(); student.NormalizedRollNumber = request.StudentId.Trim().ToUpperInvariant(); student.MajorCode = request.Major!.Trim().ToUpperInvariant(); }
    private static ManagedUserResponse ToResponse(User user, SaveManagedUserRequest? request = null) => new() { Id = user.Id, Name = user.FullName, Email = user.Email, Avatar = user.AvatarUrl, Role = user.UserRoles.FirstOrDefault()?.Role.Name.ToUpperInvariant() ?? "STUDENT", Status = ToStatus(user.Status), StudentId = user.Student?.RollNumber ?? request?.StudentId, ProgramGroup = request?.ProgramGroup, Major = user.Student?.MajorCode ?? request?.Major, Phone = user.Phone, CreatedAt = user.CreatedAt };
}

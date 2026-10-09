using EHub.Shared.Constants;

namespace EHub.Application.Features.Classes.Common;

public static class ClassAuthorizationRules
{
    public static bool IsAdmin(string? role) =>
        string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);

    public static bool IsLecturer(string? role) =>
        string.Equals(role, SystemRoles.Lecturer, StringComparison.OrdinalIgnoreCase);

    public static bool IsStaff(string? role) => IsAdmin(role) || IsLecturer(role);

    public static bool IsAssignedLecturer(
        Guid? primaryLecturerId,
        Guid currentUserId,
        string? role) =>
        IsLecturer(role) && primaryLecturerId.HasValue && primaryLecturerId.Value == currentUserId;

    public static bool CanManageClass(
        Guid? primaryLecturerId,
        Guid currentUserId,
        string? role) =>
        IsAdmin(role) || IsAssignedLecturer(primaryLecturerId, currentUserId, role);
}

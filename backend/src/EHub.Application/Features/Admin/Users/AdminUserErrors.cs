using EHub.Shared.Errors;

namespace EHub.Application.Features.Admin.Users;

public static class AdminUserErrors
{
    public static readonly Error UserNotFound = new(
        ErrorCodes.UserNotFound,
        "User not found.");

    public static readonly Error UserNotPendingApproval = new(
        ErrorCodes.ApprovalUserNotPending,
        "User is not pending approval.");

    public static readonly Error InvalidTargetRole = new(
        ErrorCodes.ApprovalInvalidTargetRole,
        "Only Lecturer or Mentor accounts can be approved or rejected.");
}

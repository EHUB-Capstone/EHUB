namespace EHub.Shared.Errors;

public static class ErrorCodes
{
    // Common error codes
    public const string CommonUnexpectedError = "COMMON_UNEXPECTED_ERROR";
    public const string CommonValidationError = "COMMON_VALIDATION_ERROR";
    public const string CommonNotFoundError = "COMMON_NOT_FOUND";
    public const string CommonConflictError = "COMMON_CONFLICT";
    public const string CommonForbiddenError = "COMMON_FORBIDDEN";
    public const string CommonUnauthorizedError = "COMMON_UNAUTHORIZED";
    public const string CommonBusinessRuleViolation = "COMMON_BUSINESS_RULE_VIOLATION";

    // Auth error codes
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthEmailAlreadyExists = "AUTH_EMAIL_ALREADY_EXISTS";
    public const string AuthUserInactive = "AUTH_USER_INACTIVE";
    public const string AuthRefreshTokenInvalid = "AUTH_REFRESH_TOKEN_INVALID";
    public const string AuthRefreshTokenExpired = "AUTH_REFRESH_TOKEN_EXPIRED";

    public const string AuthInvalidRole = "AUTH_INVALID_ROLE";
    public const string AuthAccountPendingApproval = "AUTH_ACCOUNT_PENDING_APPROVAL";
    public const string AuthAccountRejected = "AUTH_ACCOUNT_REJECTED";
    public const string AuthUserBlocked = "AUTH_USER_BLOCKED";
    public const string AuthAccountNotRegistered = "AUTH_ACCOUNT_NOT_REGISTERED";
    public const string AuthInvalidGoogleToken = "AUTH_INVALID_GOOGLE_TOKEN";
    public const string AuthGoogleEmailNotVerified = "AUTH_GOOGLE_EMAIL_NOT_VERIFIED";
    public const string AuthRefreshTokenRevoked = "AUTH_REFRESH_TOKEN_REVOKED";
    public const string AuthPasswordConfirmationMismatch = "AUTH_PASSWORD_CONFIRMATION_MISMATCH";
    public const string AuthStudentMajorRequired = "AUTH_STUDENT_MAJOR_REQUIRED";
    public const string AuthInvalidMajor = "AUTH_INVALID_MAJOR";

    // System error codes
    public const string InternalServerError = "COMMON_INTERNAL_SERVER_ERROR";

    // Admin approval error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string ApprovalUserNotPending = "APPROVAL_USER_NOT_PENDING";
    public const string ApprovalInvalidTargetRole = "APPROVAL_INVALID_TARGET_ROLE";

    // Class management error codes
    public const string ClassAccessDenied = "CLASS_ACCESS_DENIED";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string ClassArchived = "CLASS_ARCHIVED";
    public const string ClassValidationError = "VALIDATION_ERROR";
    public const string ClassScheduleConflict = "SCHEDULE_CONFLICT";
    public const string ClassConcurrencyConflict = "CLASS_CONCURRENCY_CONFLICT";
    public const string ClassInvalidLecturer = "CLASS_INVALID_LECTURER";
    public const string ClassLecturerRequired = "CLASS_LECTURER_REQUIRED";
    public const string ClassCodeDuplicated = "CLASS_CODE_DUPLICATED";
    public const string ClassIndexDuplicated = "CLASS_INDEX_DUPLICATED";
    public const string ClassBulkCreateInvalid = "CLASS_BULK_CREATE_INVALID";
    public const string ClassStudentIdentityConflict = "STUDENT_IDENTITY_CONFLICT";
    public const string ClassStudentAlreadyEnrolled = "STUDENT_ALREADY_ENROLLED";
    public const string ClassStudentNotFound = "CLASS_STUDENT_NOT_FOUND";
    public const string ClassStudentIsTeamLeader = "STUDENT_IS_TEAM_LEADER";
    public const string ClassStudentInActiveTeam = "STUDENT_IN_ACTIVE_TEAM";
    public const string ClassStudentEnrollmentConflict = "STUDENT_ENROLLMENT_CONFLICT";
    public const string ClassStudentReEnrollmentRequired = "STUDENT_RE_ENROLLMENT_REQUIRED";
    public const string ClassStudentNotDropped = "STUDENT_NOT_DROPPED";
    public const string ClassEnrollmentMajorLocked = "MAJOR_LOCKED";
    public const string ClassImportSessionInvalid = "IMPORT_SESSION_INVALID";
    public const string ClassImportSessionExpired = "IMPORT_SESSION_EXPIRED";
    public const string ClassImportSessionAlreadyProcessing = "IMPORT_SESSION_ALREADY_PROCESSING";
    public const string ClassImportNoValidRows = "IMPORT_NO_VALID_ROWS";
    public const string ClassRestoreInvalid = "CLASS_RESTORE_INVALID";
    public const string ClassChatMembershipRepairFailed = "CHAT_MEMBERSHIP_REPAIR_FAILED";

    // Team, mentor assignment, and project direction error codes
    public const string TeamNotFound = "TEAM_NOT_FOUND";
    public const string TeamInactive = "TEAM_INACTIVE";
    public const string TeamNameDuplicated = "TEAM_NAME_DUPLICATED";
    public const string TeamMembershipConflict = "TEAM_MEMBERSHIP_CONFLICT";
    public const string TeamMajorCompositionInvalid = "TEAM_MAJOR_COMPOSITION_INVALID";
    public const string TeamProposalNotFound = "TEAM_PROPOSAL_NOT_FOUND";
    public const string TeamProposalInvalid = "TEAM_PROPOSAL_INVALID";
    public const string TeamProposalStateInvalid = "TEAM_PROPOSAL_STATE_INVALID";
    public const string TeamProposalMembershipConflict = "TEAM_PROPOSAL_MEMBERSHIP_CONFLICT";
    public const string TeamApprovalConflict = "TEAM_APPROVAL_CONFLICT";
    public const string MentorNotAvailable = "MENTOR_NOT_AVAILABLE";
    public const string MentorCapacityReached = "MENTOR_CAPACITY_REACHED";
    public const string MentorAssignmentConflict = "MENTOR_ASSIGNMENT_CONFLICT";
    public const string ProjectDirectionNotFound = "PROJECT_DIRECTION_NOT_FOUND";
    public const string ProjectDirectionStateInvalid = "PROJECT_DIRECTION_STATE_INVALID";

    // Password reset error codes
    public const string AuthPasswordResetTokenInvalid = "AUTH_PASSWORD_RESET_TOKEN_INVALID";
    public const string AuthPasswordResetRateLimited = "AUTH_PASSWORD_RESET_RATE_LIMITED";
    public const string AuthPasswordResetFailed = "AUTH_PASSWORD_RESET_FAILED";
}

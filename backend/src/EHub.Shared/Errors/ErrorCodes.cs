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
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";

    // Admin approval error codes
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string ApprovalUserNotPending = "APPROVAL_USER_NOT_PENDING";
    public const string ApprovalInvalidTargetRole = "APPROVAL_INVALID_TARGET_ROLE";
}

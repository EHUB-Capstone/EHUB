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

    // System error codes
    public const string InternalServerError = "INTERNAL_SERVER_ERROR";
}

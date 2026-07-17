using EHub.Shared.Errors;

namespace EHub.Application.Features.Auth;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials = new(
        ErrorCodes.AuthInvalidCredentials,
        "Invalid email or password.");

    public static readonly Error EmailAlreadyExists = new(
        ErrorCodes.AuthEmailAlreadyExists,
        "Email already exists.");

    public static readonly Error InvalidRole = new(
        ErrorCodes.AuthInvalidRole,
        "Role must be Student, Lecturer, or Mentor.");

    public static readonly Error InvalidMajor = new(
        ErrorCodes.AuthInvalidMajor,
        "Selected major is invalid.");

    public static readonly Error StudentMajorRequired = new(
        ErrorCodes.AuthStudentMajorRequired,
        "Major is required for Student role.");

    public static readonly Error AccountPendingApproval = new(
        ErrorCodes.AuthAccountPendingApproval,
        "Your account is pending admin approval.");

    public static readonly Error AccountRejected = new(
        ErrorCodes.AuthAccountRejected,
        "Your account registration has been rejected.");

    public static readonly Error UserBlocked = new(
        ErrorCodes.AuthUserBlocked,
        "Your account has been blocked.");

    public static readonly Error UserInactive = new(
        ErrorCodes.AuthUserInactive,
        "Your account is inactive.");

    public static readonly Error AccountNotRegistered = new(
        ErrorCodes.AuthAccountNotRegistered,
        "Account is not registered. Please create an account first.");

    public static readonly Error InvalidGoogleToken = new(
        ErrorCodes.AuthInvalidGoogleToken,
        "Invalid Google token.");

    public static readonly Error GoogleEmailNotVerified = new(
        ErrorCodes.AuthGoogleEmailNotVerified,
        "Google email is not verified.");

    public static readonly Error RefreshTokenInvalid = new(
        ErrorCodes.AuthRefreshTokenInvalid,
        "Refresh token is invalid.");

    public static readonly Error RefreshTokenExpired = new(
        ErrorCodes.AuthRefreshTokenExpired,
        "Refresh token has expired.");

    public static readonly Error RefreshTokenRevoked = new(
        ErrorCodes.AuthRefreshTokenRevoked,
        "Refresh token has been revoked.");

    public static readonly Error PasswordResetTokenInvalid = new(
        ErrorCodes.AuthPasswordResetTokenInvalid,
        "Password reset token is invalid or expired.");
}

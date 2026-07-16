using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.Login;
using EHub.Application.Features.Auth.GoogleLogin;
using EHub.Application.Features.Auth.GetCurrentUser;
using EHub.Application.Features.Auth.RefreshToken;
using EHub.Application.Features.Auth.Logout;
using EHub.Application.Features.Auth.Common;
using EHub.Api.Extensions;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Shared.Errors;
using EHub.Shared.Constants;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IRegisterCommandHandler _registerCommandHandler;
    private readonly ILoginCommandHandler _loginCommandHandler;
    private readonly IGoogleLoginCommandHandler _googleLoginCommandHandler;
    private readonly IGetCurrentUserQueryHandler _getCurrentUserQueryHandler;
    private readonly IRefreshTokenCommandHandler _refreshTokenCommandHandler;
    private readonly ILogoutCommandHandler _logoutCommandHandler;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        IRegisterCommandHandler registerCommandHandler,
        ILoginCommandHandler loginCommandHandler,
        IGoogleLoginCommandHandler googleLoginCommandHandler,
        IGetCurrentUserQueryHandler getCurrentUserQueryHandler,
        IRefreshTokenCommandHandler refreshTokenCommandHandler,
        ILogoutCommandHandler logoutCommandHandler,
        IWebHostEnvironment environment)
    {
        _registerCommandHandler = registerCommandHandler;
        _loginCommandHandler = loginCommandHandler;
        _googleLoginCommandHandler = googleLoginCommandHandler;
        _getCurrentUserQueryHandler = getCurrentUserQueryHandler;
        _refreshTokenCommandHandler = refreshTokenCommandHandler;
        _logoutCommandHandler = logoutCommandHandler;
        _environment = environment;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registerCommandHandler.HandleAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                ErrorCodes.AuthEmailAlreadyExists => Conflict(
                    ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code)),

                ErrorCodes.AuthInvalidRole => BadRequest(
                    ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code)),

                ErrorCodes.AuthInvalidMajor => BadRequest(
                    ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code)),

                ErrorCodes.AuthStudentMajorRequired => BadRequest(
                    ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code)),

                _ => BadRequest(
                    ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            };
        }

        // Auto-login if registration returned access token (Student)
        if (!string.IsNullOrWhiteSpace(result.Value.AccessToken) && 
            !string.IsNullOrWhiteSpace(result.Value.RefreshToken) && 
            result.Value.ExpiresAt.HasValue)
        {
            Response.SetRefreshTokenCookie(
                result.Value.RefreshToken,
                result.Value.ExpiresAt.Value,
                _environment);
        }

        var publicResponse = new RegisterResponse
        {
            Status = result.Value.Status,
            RequiresApproval = result.Value.RequiresApproval,
            Message = result.Value.Message,
            User = result.Value.User,
            AccessToken = result.Value.AccessToken,
            ExpiresAt = result.Value.ExpiresAt
        };

        return Ok(ApiResponse<RegisterResponse>.SuccessResponse(
            publicResponse,
            result.Value.Message));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] EmailPasswordLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _loginCommandHandler.HandleAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                ErrorCodes.AuthInvalidCredentials => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountPendingApproval => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountRejected => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserBlocked => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserInactive => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                _ => BadRequest(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code))
            };
        }

        // Set HttpOnly refresh token cookie
        Response.SetRefreshTokenCookie(
            result.Value.RefreshToken,
            result.Value.RefreshTokenExpiresAt,
            _environment);

        var publicResponse = new AuthResponse
        {
            AccessToken = result.Value.AccessToken,
            ExpiresAt = result.Value.AccessTokenExpiresAt,
            User = result.Value.User
        };

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            publicResponse,
            "Login successfully"));
    }

    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(
        [FromBody] GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _googleLoginCommandHandler.HandleAsync(
            request,
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                ErrorCodes.AuthInvalidGoogleToken => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthGoogleEmailNotVerified => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountNotRegistered => NotFound(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountPendingApproval => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountRejected => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserBlocked => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserInactive => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                _ => BadRequest(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code))
            };
        }

        // Set HttpOnly refresh token cookie
        Response.SetRefreshTokenCookie(
            result.Value.RefreshToken,
            result.Value.RefreshTokenExpiresAt,
            _environment);

        var publicResponse = new AuthResponse
        {
            AccessToken = result.Value.AccessToken,
            ExpiresAt = result.Value.AccessTokenExpiresAt,
            User = result.Value.User
        };

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            publicResponse,
            "Google login successfully"));
    }

    [Authorize(Policy = SystemPolicies.AuthenticatedOnly)]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var result = await _getCurrentUserQueryHandler.HandleAsync(cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code switch
            {
                ErrorCodes.AuthAccountPendingApproval => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountRejected => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserBlocked => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserInactive => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.CommonUnauthorizedError => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                _ => BadRequest(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code))
            };
        }

        return Ok(ApiResponse<CurrentUserResponse>.SuccessResponse(
            result.Value,
            "Current user retrieved successfully"));
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        // Read refresh token from HTTP cookie
        var refreshToken = Request.Cookies[RefreshTokenCookieExtensions.RefreshTokenCookieName];

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(ApiResponse<object>.FailureResponse(
                "Refresh token is missing or invalid.",
                ErrorCodes.AuthRefreshTokenInvalid));
        }

        var result = await _refreshTokenCommandHandler.HandleAsync(
            refreshToken,
            cancellationToken);

        if (result.IsFailure)
        {
            // Clear the invalid cookie
            Response.DeleteRefreshTokenCookie(_environment);

            return result.Error.Code switch
            {
                ErrorCodes.AuthRefreshTokenInvalid => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthRefreshTokenExpired => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthRefreshTokenRevoked => Unauthorized(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountPendingApproval => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthAccountRejected => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserBlocked => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                ErrorCodes.AuthUserInactive => StatusCode(
                    StatusCodes.Status403Forbidden,
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code)),

                _ => BadRequest(
                    ApiResponse<object>.FailureResponse(
                        result.Error.Message,
                        result.Error.Code))
            };
        }

        // Set the rotated new refresh token cookie
        Response.SetRefreshTokenCookie(
            result.Value.RefreshToken,
            result.Value.RefreshTokenExpiresAt,
            _environment);

        var publicResponse = new AuthResponse
        {
            AccessToken = result.Value.AccessToken,
            ExpiresAt = result.Value.AccessTokenExpiresAt,
            User = result.Value.User
        };

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            publicResponse,
            "Token refreshed successfully"));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        // Read refresh token from HTTP cookie
        var refreshToken = Request.Cookies[RefreshTokenCookieExtensions.RefreshTokenCookieName];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await _logoutCommandHandler.HandleAsync(
                refreshToken,
                cancellationToken);
        }

        // Clean cookie from client browser
        Response.DeleteRefreshTokenCookie(_environment);

        return Ok(ApiResponse<object?>.SuccessResponse(
            null,
            "Logout successfully"));
    }
}

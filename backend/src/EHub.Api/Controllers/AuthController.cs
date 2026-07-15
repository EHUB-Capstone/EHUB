using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.Login;
using EHub.Application.Features.Auth.GoogleLogin;
using EHub.Application.Features.Auth.GetCurrentUser;
using EHub.Application.Features.Auth.RefreshToken;
using EHub.Application.Features.Auth.Logout;
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

    public AuthController(
        IRegisterCommandHandler registerCommandHandler,
        ILoginCommandHandler loginCommandHandler,
        IGoogleLoginCommandHandler googleLoginCommandHandler,
        IGetCurrentUserQueryHandler getCurrentUserQueryHandler,
        IRefreshTokenCommandHandler refreshTokenCommandHandler,
        ILogoutCommandHandler logoutCommandHandler)
    {
        _registerCommandHandler = registerCommandHandler;
        _loginCommandHandler = loginCommandHandler;
        _googleLoginCommandHandler = googleLoginCommandHandler;
        _getCurrentUserQueryHandler = getCurrentUserQueryHandler;
        _refreshTokenCommandHandler = refreshTokenCommandHandler;
        _logoutCommandHandler = logoutCommandHandler;
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

        return Ok(ApiResponse<RegisterResponse>.SuccessResponse(
            result.Value,
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

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            result.Value,
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

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            result.Value,
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
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _refreshTokenCommandHandler.HandleAsync(
            request,
            cancellationToken);

        if (result.IsFailure)
        {
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

        return Ok(ApiResponse<AuthResponse>.SuccessResponse(
            result.Value,
            "Token refreshed successfully"));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _logoutCommandHandler.HandleAsync(
            request,
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(ApiResponse<object>.FailureResponse(
                result.Error.Message,
                result.Error.Code));
        }

        return Ok(ApiResponse<object?>.SuccessResponse(
            null,
            "Logout successfully"));
    }
}

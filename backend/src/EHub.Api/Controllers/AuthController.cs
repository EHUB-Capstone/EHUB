using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.Login;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Shared.Errors;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IRegisterCommandHandler _registerCommandHandler;
    private readonly ILoginCommandHandler _loginCommandHandler;

    public AuthController(
        IRegisterCommandHandler registerCommandHandler,
        ILoginCommandHandler loginCommandHandler)
    {
        _registerCommandHandler = registerCommandHandler;
        _loginCommandHandler = loginCommandHandler;
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
}

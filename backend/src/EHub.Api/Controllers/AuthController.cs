using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using EHub.Application.Features.Auth.Register;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Shared.Errors;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IRegisterCommandHandler _registerCommandHandler;

    public AuthController(IRegisterCommandHandler registerCommandHandler)
    {
        _registerCommandHandler = registerCommandHandler;
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
}

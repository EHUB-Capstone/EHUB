using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Classes.StudentSelfService;
using EHub.Contracts.Common;
using EHub.Contracts.Teams;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize]
public sealed class StudentClassesController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public StudentClassesController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("my-classes")]
    public async Task<IActionResult> GetMyClasses([FromServices] IStudentClassSelfServiceHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetMyClassesAsync(UserId, Role, cancellationToken), "Student classes retrieved.");

    [HttpGet("my-team")]
    public async Task<IActionResult> GetMyTeam([FromServices] IStudentClassSelfServiceHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetMyTeamAsync(UserId, Role, cancellationToken), "Student team retrieved.");

    [HttpGet("my-class-detail/{classId:guid}")]
    public async Task<IActionResult> GetMyClassDetail(Guid classId, [FromServices] IStudentClassSelfServiceHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetClassDetailAsync(classId, UserId, Role, cancellationToken), "Student class detail retrieved.");

    private Guid UserId => _currentUser.UserId ?? Guid.Empty;
    private string Role => _currentUser.Roles.FirstOrDefault(role => role == SystemRoles.Student) ?? string.Empty;

    private IActionResult ToResponse<T>(EHub.Shared.Results.Result<T> result, string message)
    {
        if (result.IsSuccess) return Ok(ApiResponse<T>.SuccessResponse(result.Value, message));
        var response = ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code);
        return result.Error.Code == EHub.Shared.Errors.ErrorCodes.ClassAccessDenied
            ? StatusCode(StatusCodes.Status403Forbidden, response)
            : BadRequest(response);
    }
}

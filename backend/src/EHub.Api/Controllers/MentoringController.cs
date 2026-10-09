using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Mentoring;
using EHub.Application.Features.Mentoring.ManageProfiles;
using EHub.Contracts.Common;
using EHub.Contracts.Mentoring;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/mentoring")]
public sealed class MentoringController(ICurrentUserService currentUser, IMentorProfileHandler profiles,
    IMentoringSessionHandler sessions, IAdminMentorProfileHandler adminProfiles) : ControllerBase
{
    [HttpPost("profiles")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> CreateProfile([FromBody] SaveAdminMentorProfileRequest request, CancellationToken ct) =>
        Respond(await adminProfiles.SaveAsync(null, request, ct));

    [HttpPut("profiles/{id:guid}")]
    [Authorize(Policy = SystemPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateManagedProfile(Guid id, [FromBody] SaveAdminMentorProfileRequest request, CancellationToken ct) =>
        Respond(await adminProfiles.SaveAsync(id, request, ct));
    [HttpGet("profile")]
    [Authorize(Policy = SystemPolicies.MentorOnly)]
    public async Task<IActionResult> GetProfile(CancellationToken ct) => Respond(await profiles.GetMineAsync(UserId, ct));

    [HttpPut("profile")]
    [Authorize(Policy = SystemPolicies.MentorOnly)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateMentorProfileRequest request, CancellationToken ct) =>
        Respond(await profiles.UpdateMineAsync(UserId, request, ct));

    [HttpPost("profile/documents/{kind}")]
    [Authorize(Policy = SystemPolicies.MentorOnly)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> UploadDocument(string kind, [FromForm] IFormFile file, CancellationToken ct)
    {
        if (file is null) return BadRequest(ApiResponse<object>.FailureResponse("File is required.", ErrorCodes.CommonValidationError));
        await using var stream = file.OpenReadStream();
        return Respond(await profiles.UploadDocumentAsync(UserId, kind, stream, file.FileName, file.Length, ct));
    }

    [HttpGet("profiles/{mentorId:guid}/documents/{kind}")]
    public async Task<IActionResult> DownloadDocument(Guid mentorId, string kind, CancellationToken ct)
    {
        var result = await profiles.DownloadDocumentAsync(mentorId, kind, UserId, Role, ct);
        return result.IsSuccess ? File(result.Value.Content, result.Value.ContentType, result.Value.FileName) : Respond(result);
    }

    [HttpGet("directory")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public async Task<IActionResult> GetDirectory(CancellationToken ct) =>
        Respond(await profiles.GetDirectoryAsync(UserId, Role, ct));

    [HttpGet("teams/{teamId:guid}/recommendations")]
    [Authorize(Policy = SystemPolicies.StaffOnly)]
    public async Task<IActionResult> Recommend(Guid teamId, CancellationToken ct) =>
        Respond(await profiles.RecommendAsync(teamId, UserId, Role, ct));

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] Guid? teamId, CancellationToken ct) =>
        Respond(await sessions.ListAsync(teamId, UserId, Role, ct));

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession([FromBody] SaveMentoringSessionRequest request, CancellationToken ct) =>
        Respond(await sessions.CreateAsync(request, UserId, Role, ct));

    [HttpPut("sessions/{id:guid}")]
    public async Task<IActionResult> UpdateSession(Guid id, [FromBody] SaveMentoringSessionRequest request, CancellationToken ct) =>
        Respond(await sessions.UpdateAsync(id, request, UserId, Role, ct));

    [HttpPost("sessions/{id:guid}/complete")]
    public async Task<IActionResult> CompleteSession(Guid id, [FromBody] SaveMentoringNotesRequest request, CancellationToken ct) =>
        Respond(await sessions.CompleteAsync(id, request, UserId, Role, ct));

    [HttpPost("sessions/{id:guid}/cancel")]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken ct) =>
        Respond(await sessions.CancelAsync(id, UserId, Role, ct));

    [HttpPost("sessions/{id:guid}/action-items")]
    public async Task<IActionResult> AddActionItem(Guid id, [FromBody] CreateMentoringActionItemRequest request, CancellationToken ct) =>
        Respond(await sessions.AddActionItemAsync(id, request, UserId, Role, ct));

    [HttpPut("sessions/{id:guid}/feedback")]
    [Authorize(Policy = SystemPolicies.StudentOnly)]
    public async Task<IActionResult> SaveFeedback(Guid id, [FromBody] SaveMentoringFeedbackRequest request, CancellationToken ct) =>
        Respond(await sessions.SaveFeedbackAsync(id, request, UserId, Role, ct));

    [HttpGet("sessions/{id:guid}/feedback")]
    public async Task<IActionResult> GetFeedback(Guid id, CancellationToken ct) =>
        Respond(await sessions.GetFeedbackAsync(id, UserId, Role, ct));

    private Guid UserId => currentUser.UserId ?? Guid.Empty;
    private string Role => currentUser.Roles.Contains(SystemRoles.Admin) ? SystemRoles.Admin :
        currentUser.Roles.Contains(SystemRoles.Lecturer) ? SystemRoles.Lecturer :
        currentUser.Roles.Contains(SystemRoles.Mentor) ? SystemRoles.Mentor :
        currentUser.Roles.Contains(SystemRoles.Student) ? SystemRoles.Student : string.Empty;
    private IActionResult Respond<T>(Result<T> result) => result.IsSuccess
        ? Ok(ApiResponse<T>.SuccessResponse(result.Value!))
        : result.Error.Code == ErrorCodes.ClassAccessDenied
            ? StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : result.Error.Code == ErrorCodes.MentorMatchingUnavailable
                ? StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
            : result.Error.Code.EndsWith("NOT_FOUND", StringComparison.OrdinalIgnoreCase)
                ? NotFound(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code))
                : BadRequest(ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code));
}

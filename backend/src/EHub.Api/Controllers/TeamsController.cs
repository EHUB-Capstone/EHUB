using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.Teams.ManageTeams;
using EHub.Application.Features.Teams.MentorAssignments;
using EHub.Application.Features.Teams.ProjectDirections;
using EHub.Application.Features.Teams.TeamProposals;
using EHub.Contracts.Common;
using EHub.Contracts.Teams;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class TeamsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public TeamsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("teams")]
    public async Task<IActionResult> GetAccessibleTeams([FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAccessibleAsync(UserId, Role, cancellationToken), "Accessible teams retrieved.");

    [HttpGet("classes/{classId:guid}/teams")]
    public async Task<IActionResult> GetClassTeams(Guid classId, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetForClassAsync(classId, UserId, Role, cancellationToken), "Class teams retrieved.");

    [HttpPost("classes/{classId:guid}/teams")]
    public async Task<IActionResult> CreateTeam(Guid classId, [FromBody] CreateTeamRequest request, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateAsync(classId, request, UserId, Role, cancellationToken), "Team created.");

    [HttpPost("classes/{classId:guid}/teams/generate")]
    public async Task<IActionResult> GenerateTeam(Guid classId, [FromBody] GenerateClassTeamRequest request, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GenerateAsync(classId, request, UserId, Role, cancellationToken), "Team request processed.");

    [HttpPost("classes/{classId:guid}/teams/student-proposal")]
    public async Task<IActionResult> SubmitStudentProposal(Guid classId, [FromBody] SubmitStudentTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.SubmitStudentProposalAsync(classId, request, UserId, Role, cancellationToken), "Team proposal submitted.");

    [HttpGet("teams/{teamId:guid}")]
    public async Task<IActionResult> GetTeam(Guid teamId, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAsync(teamId, UserId, Role, cancellationToken), "Team retrieved.");

    [HttpPut("teams/{teamId:guid}/members")]
    public async Task<IActionResult> UpdateMembers(Guid teamId, [FromBody] UpdateTeamMembersRequest request, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateMembersAsync(teamId, request, UserId, Role, cancellationToken), "Team members updated.");

    [HttpPut("teams/{teamId:guid}/leader")]
    public async Task<IActionResult> AssignLeader(Guid teamId, [FromBody] AssignTeamLeaderRequest request, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.AssignLeaderAsync(teamId, request, UserId, Role, cancellationToken), "Team leader assigned.");

    [HttpDelete("teams/{teamId:guid}")]
    public async Task<IActionResult> DeleteTeam(Guid teamId, [FromServices] ITeamManagementHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.DeleteAsync(teamId, UserId, Role, cancellationToken), "Team archived and members unassigned.");

    [HttpGet("classes/{classId:guid}/mentors")]
    public async Task<IActionResult> GetClassMentors(Guid classId, [FromServices] IMentorAssignmentHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetForClassAsync(classId, UserId, Role, cancellationToken), "Class mentors retrieved.");

    [HttpGet("classes/{classId:guid}/mentor-candidates")]
    public async Task<IActionResult> GetMentorCandidates(Guid classId, [FromServices] IMentorAssignmentHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetCandidatesAsync(classId, UserId, Role, cancellationToken), "Mentor candidates retrieved.");

    [HttpGet("teams/{teamId:guid}/mentor-assignments")]
    public async Task<IActionResult> GetMentorHistory(Guid teamId, [FromServices] IMentorAssignmentHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetForTeamAsync(teamId, UserId, Role, cancellationToken), "Mentor assignment history retrieved.");

    [HttpPost("teams/{teamId:guid}/mentor-assignments")]
    public async Task<IActionResult> AssignMentor(Guid teamId, [FromBody] AssignMentorRequest request, [FromServices] IMentorAssignmentHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.AssignAsync(teamId, request, UserId, Role, cancellationToken), "Mentor assigned.");

    [HttpPost("teams/{teamId:guid}/mentor-assignments/end")]
    public async Task<IActionResult> EndMentor(Guid teamId, [FromBody] EndMentorAssignmentRequest request, [FromServices] IMentorAssignmentHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.EndAsync(teamId, request, UserId, Role, cancellationToken), "Mentor assignment ended.");

    [HttpGet("classes/{classId:guid}/team-proposals")]
    public async Task<IActionResult> GetProposals(Guid classId, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetForClassAsync(classId, UserId, Role, cancellationToken), "Team proposals retrieved.");

    [HttpPost("classes/{classId:guid}/team-proposals")]
    public async Task<IActionResult> CreateProposal(Guid classId, [FromBody] CreateTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateAsync(classId, request, UserId, Role, cancellationToken), "Team proposal draft created.");

    [HttpPut("team-proposals/{proposalId:guid}")]
    public async Task<IActionResult> UpdateProposal(Guid proposalId, [FromBody] UpdateTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateAsync(proposalId, request, UserId, Role, cancellationToken), "Team proposal updated.");

    [HttpPost("team-proposals/{proposalId:guid}/submit")]
    public async Task<IActionResult> SubmitProposal(Guid proposalId, [FromBody] SubmitTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.SubmitAsync(proposalId, request, UserId, Role, cancellationToken), "Team proposal submitted.");

    [HttpPost("team-proposals/{proposalId:guid}/cancel")]
    public async Task<IActionResult> CancelProposal(Guid proposalId, [FromBody] CancelTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.CancelAsync(proposalId, request, UserId, Role, cancellationToken), "Team proposal cancelled.");

    [HttpPost("team-proposals/{proposalId:guid}/review")]
    public async Task<IActionResult> ReviewProposal(Guid proposalId, [FromBody] ReviewTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.ReviewAsync(proposalId, request, UserId, Role, cancellationToken), "Team proposal reviewed.");

    [HttpPut("teams/{teamId:guid}/review")]
    public async Task<IActionResult> ReviewTeam(Guid teamId, [FromBody] ReviewTeamProposalRequest request, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.ReviewAsync(teamId, request, UserId, Role, cancellationToken), "Team proposal reviewed.");

    [HttpGet("team-proposals/{proposalId:guid}/history")]
    public async Task<IActionResult> GetProposalHistory(Guid proposalId, [FromServices] ITeamProposalHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetHistoryAsync(proposalId, UserId, Role, cancellationToken), "Team proposal history retrieved.");

    [HttpGet("teams/{teamId:guid}/project-direction")]
    public async Task<IActionResult> GetDirection(Guid teamId, [FromServices] IProjectDirectionHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAsync(teamId, UserId, Role, cancellationToken), "Project direction retrieved.");

    [HttpPut("teams/{teamId:guid}/project-direction")]
    public async Task<IActionResult> SaveDirection(Guid teamId, [FromBody] SaveProjectDirectionRequest request, [FromServices] IProjectDirectionHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.SaveAsync(teamId, request, UserId, Role, cancellationToken), "Project direction saved.");

    [HttpPost("teams/{teamId:guid}/project-direction/submit")]
    public async Task<IActionResult> SubmitDirection(Guid teamId, [FromBody] ProjectDirectionStateRequest request, [FromServices] IProjectDirectionHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.SubmitAsync(teamId, request, UserId, Role, cancellationToken), "Project direction submitted.");

    [HttpPost("teams/{teamId:guid}/project-direction/review")]
    public async Task<IActionResult> ReviewDirection(Guid teamId, [FromBody] ReviewProjectDirectionRequest request, [FromServices] IProjectDirectionHandler handler, CancellationToken cancellationToken) =>
        ToResponse(await handler.ReviewAsync(teamId, request, UserId, Role, cancellationToken), "Project direction reviewed.");

    private Guid UserId => _currentUser.UserId ?? Guid.Empty;
    private string Role => _currentUser.Roles.FirstOrDefault(role =>
        role is SystemRoles.Admin or SystemRoles.Lecturer or SystemRoles.Mentor or SystemRoles.Student) ?? string.Empty;

    private IActionResult ToResponse<T>(EHub.Shared.Results.Result<T> result, string message)
    {
        if (result.IsSuccess) return Ok(ApiResponse<T>.SuccessResponse(result.Value, message));
        return ErrorResponse(result.Error);
    }

    private IActionResult ToResponse(EHub.Shared.Results.Result result, string message)
    {
        if (result.IsSuccess) return Ok(ApiResponse<object?>.SuccessResponse(null, message));
        return ErrorResponse(result.Error);
    }

    private IActionResult ErrorResponse(Error error)
    {
        var response = ApiResponse<object>.FailureResponse(error.Message, error.Code);
        if (error.Code == ErrorCodes.ClassAccessDenied) return StatusCode(StatusCodes.Status403Forbidden, response);
        if (error.Code.EndsWith("_NOT_FOUND", StringComparison.OrdinalIgnoreCase)) return NotFound(response);
        if (error.Code.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase) ||
            error.Code.Contains("DUPLICATED", StringComparison.OrdinalIgnoreCase) ||
            error.Code.Contains("STATE_INVALID", StringComparison.OrdinalIgnoreCase)) return Conflict(response);
        return BadRequest(response);
    }
}

using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Features.ProjectProposals;
using EHub.Application.Features.ProposalAnalyses;
using EHub.Contracts.Common;
using EHub.Contracts.ProjectProposals;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/workspace")]
[Authorize]
public sealed class ProjectProposalsController : ControllerBase
{
    private readonly ICurrentUserService _currentUser;

    public ProjectProposalsController(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    [HttpGet("teams/{teamId:guid}/proposal")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetByTeam(
        Guid teamId,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetByTeamAsync(teamId, UserId, Role, cancellationToken), "Project proposal retrieved.");

    [HttpPost("teams/{teamId:guid}/proposal")]
    public async Task<IActionResult> Create(
        Guid teamId,
        [FromBody] CreateProjectProposalRequest request,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.CreateAsync(teamId, request, UserId, Role, cancellationToken), "Project proposal draft created.", created: true);

    [HttpPut("proposals/{proposalId:guid}")]
    public async Task<IActionResult> Update(
        Guid proposalId,
        [FromBody] UpdateProjectProposalRequest request,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.UpdateAsync(proposalId, request, UserId, Role, cancellationToken), "Project proposal draft version saved.");

    [HttpPost("proposals/{proposalId:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid proposalId,
        [FromBody] SubmitProjectProposalRequest request,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.SubmitAsync(proposalId, request, UserId, Role, cancellationToken), "Project proposal submitted.");

    [HttpGet("proposals/{proposalId:guid}/versions")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetVersions(
        Guid proposalId,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetVersionsAsync(proposalId, UserId, Role, cancellationToken), "Project proposal versions retrieved.");

    [HttpGet("proposals/{proposalId:guid}/versions/{versionId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetVersion(
        Guid proposalId,
        Guid versionId,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetVersionAsync(proposalId, versionId, UserId, Role, cancellationToken), "Project proposal version retrieved.");

    [HttpPost("proposals/{proposalId:guid}/versions/{versionId:guid}/restore")]
    public async Task<IActionResult> RestoreVersion(
        Guid proposalId,
        Guid versionId,
        [FromBody] RestoreProjectProposalVersionRequest request,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.RestoreVersionAsync(proposalId, versionId, request, UserId, Role, cancellationToken), "Project proposal version restored as a new draft.");

    [HttpPost("proposals/{proposalId:guid}/review")]
    public async Task<IActionResult> Review(
        Guid proposalId,
        [FromBody] ReviewProjectProposalRequest request,
        [FromServices] IProjectProposalHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.ReviewAsync(proposalId, request, UserId, Role, cancellationToken), "Project proposal reviewed.");

    [HttpGet("proposal-analyses/{jobId:guid}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetAnalysis(
        Guid jobId,
        [FromServices] IProjectProposalAnalysisQueryHandler handler,
        CancellationToken cancellationToken) =>
        ToResponse(await handler.GetAsync(jobId, UserId, Role, cancellationToken), "Project proposal analysis retrieved.");

    private Guid UserId => _currentUser.UserId ?? Guid.Empty;

    private string Role => SystemRoles.All.FirstOrDefault(expected =>
        _currentUser.Roles.Any(role => string.Equals(role, expected, StringComparison.OrdinalIgnoreCase))) ?? string.Empty;

    private IActionResult ToResponse<T>(Result<T> result, string message, bool created = false)
    {
        if (result.IsSuccess)
        {
            var response = ApiResponse<T>.SuccessResponse(result.Value, message);
            return created ? StatusCode(StatusCodes.Status201Created, response) : Ok(response);
        }

        var failure = ApiResponse<object>.FailureResponse(result.Error.Message, result.Error.Code);
        return result.Error.Code switch
        {
            ErrorCodes.ProjectProposalAccessDenied or ErrorCodes.ProjectProposalAnalysisAccessDenied => StatusCode(StatusCodes.Status403Forbidden, failure),
            ErrorCodes.TeamNotFound or ErrorCodes.WorkspaceNotFound or ErrorCodes.ProjectProposalNotFound or ErrorCodes.ProjectProposalVersionNotFound
                or ErrorCodes.ProjectProposalAnalysisNotFound or ErrorCodes.AiFeatureDisabled => NotFound(failure),
            ErrorCodes.ProjectProposalConcurrencyConflict or ErrorCodes.ProjectProposalStateInvalid or ErrorCodes.ProjectProposalDirectionNotApproved
                or ErrorCodes.TeamInactive or ErrorCodes.ClassArchived or ErrorCodes.ClassCompleted => Conflict(failure),
            _ => BadRequest(failure)
        };
    }
}

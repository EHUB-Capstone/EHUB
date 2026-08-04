using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Common;
using EHub.Contracts.Dashboard;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EHub.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = SystemPolicies.AdminOnly)]
public sealed class DashboardController(IApplicationDbContext context) : ControllerBase
{
    [HttpGet("admin")]
    public async Task<IActionResult> GetAdminDashboard(CancellationToken cancellationToken)
    {
        var totalTasks = await context.SprintTasks.CountAsync(cancellationToken);
        var completedTasks = await context.SprintTasks.CountAsync(task => task.Status == SprintTaskStatus.Done, cancellationToken);
        var topTeams = await context.Evaluations.AsNoTracking()
            .Where(evaluation => evaluation.Status != EvaluationStatus.Draft && evaluation.MaxTotalScore > 0)
            .GroupBy(evaluation => new { evaluation.Project.Team.TeamName, evaluation.Project.Team.Class.ClassCode, evaluation.Project.Name })
            .Select(group => new TopTeamResponse
            {
                StartupName = group.Key.Name,
                Team = new TeamSummaryResponse { Name = group.Key.TeamName, ClassId = new ClassSummaryResponse { ClassCode = group.Key.ClassCode } },
                AvgScore = Math.Round(group.Average(evaluation => evaluation.TotalScore / evaluation.MaxTotalScore * 10m), 2),
            })
            .OrderByDescending(team => team.AvgScore)
            .Take(10)
            .ToArrayAsync(cancellationToken);

        var response = new AdminDashboardResponse
        {
            Stats = new AdminDashboardStatsResponse
            {
                TotalUsers = await context.Users.CountAsync(cancellationToken),
                TotalClasses = await context.Classes.CountAsync(cancellationToken),
                TotalTeams = await context.Teams.CountAsync(cancellationToken),
                TotalIdeas = await context.Projects.CountAsync(cancellationToken),
                TotalEvaluations = await context.Evaluations.CountAsync(cancellationToken),
                SubmittedProposals = await context.ProjectProposals.CountAsync(proposal => proposal.Status == ProjectProposalStatus.Submitted, cancellationToken),
                TotalMentoringSessions = await context.MentoringSessions.CountAsync(cancellationToken),
                TotalTasks = totalTasks,
                CompletedTasks = completedTasks,
                OverallTaskProgress = totalTasks == 0 ? 0 : Math.Round(completedTasks * 100m / totalTasks, 2),
            },
            UsersByRole = await context.UserRoles.AsNoTracking().GroupBy(item => item.Role.Name)
                .Select(group => new RoleCountResponse { Role = group.Key.ToUpper(), Count = group.Count() }).ToArrayAsync(cancellationToken),
            IdeasByStatus = await context.Projects.AsNoTracking().GroupBy(item => item.Status)
                .Select(group => new StatusCountResponse { Status = group.Key.ToString().ToUpper(), Count = group.Count() }).ToArrayAsync(cancellationToken),
            TopTeams = topTeams,
        };

        return Ok(ApiResponse<AdminDashboardResponse>.SuccessResponse(response, "Admin dashboard retrieved successfully."));
    }
}

using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Dashboard;
using EHub.Domain.Enums;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Dashboard.GetAdminDashboard;

public sealed class GetAdminDashboardQueryHandler(IApplicationDbContext context) : IGetAdminDashboardQueryHandler
{
    public async Task<Result<AdminDashboardResponse>> HandleAsync(CancellationToken cancellationToken = default)
    {
        var totalTasks = await context.SprintTasks.CountAsync(cancellationToken);
        var completedTasks = await context.SprintTasks.CountAsync(task => task.Status == SprintTaskStatus.Done, cancellationToken);
        var topTeams = await context.Evaluations.AsNoTracking().Where(item => item.Status != EvaluationStatus.Draft && item.MaxTotalScore > 0)
            .GroupBy(item => new { item.Project.Team.TeamName, item.Project.Team.Class.ClassCode, item.Project.Name })
            .Select(group => new TopTeamResponse { StartupName = group.Key.Name, Team = new TeamSummaryResponse { Name = group.Key.TeamName, ClassId = new ClassSummaryResponse { ClassCode = group.Key.ClassCode } }, AvgScore = Math.Round(group.Average(item => item.TotalScore / item.MaxTotalScore * 10m), 2) })
            .OrderByDescending(item => item.AvgScore).Take(10).ToArrayAsync(cancellationToken);
        return Result.Success(new AdminDashboardResponse
        {
            Stats = new AdminDashboardStatsResponse { TotalUsers = await context.Users.CountAsync(cancellationToken), TotalClasses = await context.Classes.CountAsync(cancellationToken), TotalTeams = await context.Teams.CountAsync(cancellationToken), TotalIdeas = await context.Projects.CountAsync(cancellationToken), TotalEvaluations = await context.Evaluations.CountAsync(cancellationToken), SubmittedProposals = await context.ProjectProposals.CountAsync(item => item.Status == ProjectProposalStatus.Submitted, cancellationToken), TotalMentoringSessions = await context.MentoringSessions.CountAsync(cancellationToken), TotalTasks = totalTasks, CompletedTasks = completedTasks, OverallTaskProgress = totalTasks == 0 ? 0 : Math.Round(completedTasks * 100m / totalTasks, 2) },
            UsersByRole = await context.UserRoles.AsNoTracking().GroupBy(item => item.Role.Name).Select(group => new RoleCountResponse { Role = group.Key.ToUpper(), Count = group.Count() }).ToArrayAsync(cancellationToken),
            IdeasByStatus = await context.Projects.AsNoTracking().GroupBy(item => item.Status).Select(group => new StatusCountResponse { Status = group.Key.ToString().ToUpper(), Count = group.Count() }).ToArrayAsync(cancellationToken),
            TopTeams = topTeams,
        });
    }
}

namespace EHub.Contracts.Dashboard;

public sealed class AdminDashboardResponse
{
    public AdminDashboardStatsResponse Stats { get; init; } = new();
    public IReadOnlyCollection<RoleCountResponse> UsersByRole { get; init; } = Array.Empty<RoleCountResponse>();
    public IReadOnlyCollection<StatusCountResponse> IdeasByStatus { get; init; } = Array.Empty<StatusCountResponse>();
    public IReadOnlyCollection<TopTeamResponse> TopTeams { get; init; } = Array.Empty<TopTeamResponse>();
}

public sealed class AdminDashboardStatsResponse
{
    public int TotalUsers { get; init; }
    public int TotalClasses { get; init; }
    public int TotalTeams { get; init; }
    public int TotalIdeas { get; init; }
    public int TotalEvaluations { get; init; }
    public int SubmittedProposals { get; init; }
    public int TotalMentoringSessions { get; init; }
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public decimal OverallTaskProgress { get; init; }
}

public sealed class RoleCountResponse { public string Role { get; init; } = string.Empty; public int Count { get; init; } }
public sealed class StatusCountResponse { public string Status { get; init; } = string.Empty; public int Count { get; init; } }

public sealed class TopTeamResponse
{
    public string StartupName { get; init; } = string.Empty;
    public TeamSummaryResponse Team { get; init; } = new();
    public decimal AvgScore { get; init; }
}

public sealed class TeamSummaryResponse { public string Name { get; init; } = string.Empty; public ClassSummaryResponse ClassId { get; init; } = new(); }
public sealed class ClassSummaryResponse { public string ClassCode { get; init; } = string.Empty; }

public sealed class AuthStatsResponse
{
    public int TotalUsers { get; init; }
    public int TotalRegisters { get; init; }
    public int TotalLogins { get; init; }
    public int FailedLogins { get; init; }
    public int TodayRegisters { get; init; }
    public int TodayLogins { get; init; }
    public int ActiveUsersToday { get; init; }
    public IReadOnlyCollection<DateCountResponse> LoginRate { get; init; } = Array.Empty<DateCountResponse>();
    public IReadOnlyCollection<DateCountResponse> RegisterRate { get; init; } = Array.Empty<DateCountResponse>();
}

public sealed class DateCountResponse { public string Date { get; init; } = string.Empty; public int Count { get; init; } }

public sealed class OnlineUsersResponse
{
    public int OnlineCount { get; init; }
    public int TotalUsers { get; init; }
    public IReadOnlyCollection<ActivityUserResponse> OnlineUsers { get; init; } = Array.Empty<ActivityUserResponse>();
    public IReadOnlyCollection<ActivityUserResponse> RecentlyActive { get; init; } = Array.Empty<ActivityUserResponse>();
}

public sealed class ActivityUserResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public string Role { get; init; } = "STUDENT";
    public DateTime? LastSeen { get; init; }
}

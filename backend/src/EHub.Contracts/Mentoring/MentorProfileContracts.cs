namespace EHub.Contracts.Mentoring;

public sealed class MentorProfileResponse
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string MentorType { get; init; } = string.Empty;
    public string[] Expertise { get; init; } = [];
    public string? Bio { get; init; }
    public string? Experience { get; init; }
    public string? Organization { get; init; }
    public string? LinkedInUrl { get; init; }
    public string? PortfolioUrl { get; init; }
    public string? CvFileName { get; init; }
    public string? PortfolioFileName { get; init; }
    public int MaxTeams { get; init; }
    public string Status { get; init; } = string.Empty;
    public int ActiveTeamCount { get; init; }
    public int TotalAssignments { get; init; }
    public int TotalSessions { get; init; }
    public double? AverageFeedbackRating { get; init; }
}

public sealed class UpdateMentorProfileRequest
{
    public string MentorType { get; init; } = string.Empty;
    public string[] Expertise { get; init; } = [];
    public string? Bio { get; init; }
    public string? Experience { get; init; }
    public string? Organization { get; init; }
    public string? LinkedInUrl { get; init; }
    public string? PortfolioUrl { get; init; }
}

public sealed class MentorRecommendationResponse
{
    public MentorProfileResponse Mentor { get; init; } = new();
    public int FitScore { get; init; }
    public string[] Reasons { get; init; } = [];
    public int ActiveTeamCount { get; init; }
    public bool HasCapacity { get; init; }
}

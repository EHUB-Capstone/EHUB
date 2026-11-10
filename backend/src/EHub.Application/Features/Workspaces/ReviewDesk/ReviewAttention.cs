namespace EHub.Application.Features.Workspaces.ReviewDesk;

/// <summary>The reasons a team is listed on the review inbox. They only order the list; none is a judgement of the team's work.</summary>
internal static class ReviewAttention
{
    public const string Overdue = "Overdue";
    public const string DueSoon = "DueSoon";
    public const string NewVersion = "NewVersion";

    /// <summary>The checkpoint is "due soon" this long before its deadline.</summary>
    public static readonly TimeSpan DueSoonWindow = TimeSpan.FromHours(48);
}

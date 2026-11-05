using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.Domain.Enums;

namespace EHub.Application.Features.Showcase;

internal static class ShowcaseMapper
{
    public static ShowcaseProfileDto ToProfile(
        string? projectName, string? description, string? problem, string? solution, string? targetUsers) => new()
    {
        ProjectName = projectName ?? string.Empty,
        Description = description ?? string.Empty,
        Problem = problem,
        Solution = solution,
        TargetUsers = targetUsers
    };

    /// <param name="profile">Live Project Profile values; an empty profile is used when the project no longer exists.</param>
    /// <param name="includeReviewNote">False for ordinary viewers: review feedback is only for the team and moderators.</param>
    public static ShowcaseDto ToDto(
        ShowcaseProject showcase, ShowcaseProfileDto? profile, bool canEdit, bool includeReviewNote = true) => new()
    {
        Id = showcase.Id,
        ProjectLineageId = showcase.ProjectLineageId,
        Profile = profile ?? new ShowcaseProfileDto(),
        LogoUrl = showcase.LogoUrl,
        Highlights = showcase.Highlights,
        DevelopmentStage = showcase.DevelopmentStage.ToString(),
        Status = showcase.Status.ToString(),
        ShowTeamMembers = showcase.ShowTeamMembers,
        ReviewNote = includeReviewNote ? showcase.ReviewNote : null,
        PublishedAt = showcase.PublishedAt,
        IsFeatured = showcase.IsFeatured,
        FeaturedAt = showcase.FeaturedAt,
        ViewCount = showcase.ViewCount,
        LikeCount = showcase.LikeCount,
        CommentCount = showcase.CommentCount,
        RowVersion = showcase.Version,
        CreatedAt = showcase.CreatedAt,
        UpdatedAt = showcase.UpdatedAt,
        CanEdit = canEdit && showcase.Status != ShowcaseStatus.PendingReview,
        Links = showcase.Links.OrderBy(link => link.SortOrder)
            .Select(link => new ShowcaseLinkDto
            {
                Id = link.Id,
                LinkType = link.LinkType.ToString(),
                Label = link.Label,
                Url = link.Url
            }).ToArray(),
        Media = showcase.Media.OrderBy(media => media.SortOrder)
            .Select(ToDto).ToArray()
    };

    public static ShowcaseMediaDto ToDto(ShowcaseMedia media) => new()
    {
        Id = media.Id,
        MediaType = media.MediaType.ToString(),
        Url = media.Url,
        Caption = media.Caption,
        SortOrder = media.SortOrder
    };
}

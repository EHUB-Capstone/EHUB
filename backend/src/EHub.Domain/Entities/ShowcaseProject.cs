using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// Public-facing page of a project. It is keyed by <see cref="ProjectLineageId"/> so a single showcase survives
/// across semesters. Project name, description, problem, solution and target users are NOT stored here: they are read
/// from the latest Project in the lineage (the Project Profile), so the two pages can never disagree.
/// </summary>
public class ShowcaseProject : AuditableEntity
{
    public Guid ProjectLineageId { get; set; }

    public string? LogoUrl { get; set; }
    public string? Highlights { get; set; }

    public ShowcaseDevelopmentStage DevelopmentStage { get; set; } = ShowcaseDevelopmentStage.Idea;
    public ShowcaseStatus Status { get; set; } = ShowcaseStatus.Draft;
    public bool ShowTeamMembers { get; set; } = true;

    public DateTime? PublishedAt { get; set; }
    public string? ReviewNote { get; set; }

    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
    public int CommentCount { get; set; }

    // PostgreSQL optimistic concurrency token mapped to the system xmin column.
    public uint Version { get; set; }

    public virtual ICollection<ShowcaseLink> Links { get; set; } = new List<ShowcaseLink>();
    public virtual ICollection<ShowcaseMedia> Media { get; set; } = new List<ShowcaseMedia>();
    public virtual ICollection<ShowcaseReviewHistory> ReviewHistory { get; set; } = new List<ShowcaseReviewHistory>();
}

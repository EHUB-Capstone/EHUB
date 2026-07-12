using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectProposal : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public Guid ClassId { get; set; }
    public virtual Class Class { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? StartupName { get; set; }
    public string? Tagline { get; set; }

    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public string? TargetCustomers { get; set; }
    public string? ValueProposition { get; set; }
    public string? MarketSize { get; set; }
    public string? Competitors { get; set; }
    public string? BusinessModel { get; set; }
    public string? RevenueModel { get; set; }
    public string? MarketingStrategy { get; set; }
    public string? Technology { get; set; }
    public string? FinancialPlan { get; set; }
    public string? Roadmap { get; set; }
    public string? TeamIntroduction { get; set; }

    public ProjectProposalStatus Status { get; set; } = ProjectProposalStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    public Guid? UpdatedById { get; set; }
    public virtual User? Updater { get; set; }

    // Navigation properties
    public virtual ICollection<ProjectProposalVersion> Versions { get; set; } = new List<ProjectProposalVersion>();
    public virtual ICollection<ProjectComment> Comments { get; set; } = new List<ProjectComment>();
    public virtual ICollection<PitchDeck> PitchDecks { get; set; } = new List<PitchDeck>();
}

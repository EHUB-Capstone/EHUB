using System;
using System.Collections.Generic;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class Project : AuditableEntity
{
    public Guid TeamId { get; set; }
    public virtual Team Team { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Problem { get; set; }
    public string? Solution { get; set; }
    public string? StartupField { get; set; }
    public string? BusinessModel { get; set; }
    public string? Technology { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;
    public bool IsHighPotential { get; set; } = false;

    public Guid? CreatedById { get; set; }
    public virtual User? Creator { get; set; }

    public DateTime? SubmittedAt { get; set; }

    // Navigation properties
    public virtual ICollection<ProjectTag> ProjectTags { get; set; } = new List<ProjectTag>();
    public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    public virtual ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
    public virtual ICollection<MentorAssignment> MentorAssignments { get; set; } = new List<MentorAssignment>();
    public virtual ICollection<AcademicDataset> AcademicDatasets { get; set; } = new List<AcademicDataset>();
    public virtual ProjectProposal? ProjectProposal { get; set; }
    public virtual ICollection<PitchDeck> PitchDecks { get; set; } = new List<PitchDeck>();
    public virtual ICollection<ProjectShortcut> Shortcuts { get; set; } = new List<ProjectShortcut>();
    public virtual ICollection<StartupLineage> OriginalLineages { get; set; } = new List<StartupLineage>();
    public virtual ICollection<StartupLineage> CurrentLineages { get; set; } = new List<StartupLineage>();
}

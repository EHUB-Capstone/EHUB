using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectAnalysis : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public ProjectAnalysisType AnalysisType { get; set; } = ProjectAnalysisType.RuleBased;

    public string StrengthsJson { get; set; } = "[]";
    public string WeaknessesJson { get; set; } = "[]";

    public string? FeasibilityAnalysis { get; set; }
    public string? MarketPotential { get; set; }

    public string RisksJson { get; set; } = "[]";
    public string SimilarIdeasJson { get; set; } = "[]";
    public string SuggestionsJson { get; set; } = "[]";

    public decimal? Score { get; set; }
    public string? Model { get; set; }

    public Guid? GeneratedById { get; set; }
    public virtual User? GeneratedBy { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

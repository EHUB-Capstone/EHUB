using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

/// <summary>
/// One competition a project took part in during one year, with the furthest stage it reached.
/// <see cref="Competition"/> is a name from the fixed list (or "Others", then <see cref="OtherName"/> is the free text).
/// </summary>
public class ProjectCompetitionEntry : BaseEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public string Competition { get; set; } = string.Empty;
    public string? OtherName { get; set; }

    /// <summary>Lower-cased, single-spaced <see cref="OtherName"/>, or empty for a listed competition; part of the unique key.</summary>
    public string NormalizedOtherName { get; set; } = string.Empty;

    /// <summary>The year the competition took place (not the academic semester of the project).</summary>
    public int Year { get; set; }

    public CompetitionStage Stage { get; set; } = CompetitionStage.Registered;

    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

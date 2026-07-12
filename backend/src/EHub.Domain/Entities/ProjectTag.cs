using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ProjectTag : BaseEntity
{
    public Guid ProjectId { get; set; }
    public virtual Project Project { get; set; } = null!;

    public string TagName { get; set; } = string.Empty;
    public string NormalizedTagName { get; set; } = string.Empty;
    public ProjectTagType TagType { get; set; } = ProjectTagType.Keyword;

    public Guid? CreatedById { get; set; }
    public virtual User? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

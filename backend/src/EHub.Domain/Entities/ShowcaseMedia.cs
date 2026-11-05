using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ShowcaseMedia : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public virtual ShowcaseProject ShowcaseProject { get; set; } = null!;

    public ShowcaseMediaType MediaType { get; set; } = ShowcaseMediaType.Image;
    public string Url { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

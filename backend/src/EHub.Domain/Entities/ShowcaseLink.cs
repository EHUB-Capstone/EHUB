using System;
using EHub.Domain.Common;
using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

public class ShowcaseLink : BaseEntity
{
    public Guid ShowcaseProjectId { get; set; }
    public virtual ShowcaseProject ShowcaseProject { get; set; } = null!;

    public ShowcaseLinkType LinkType { get; set; } = ShowcaseLinkType.Website;
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

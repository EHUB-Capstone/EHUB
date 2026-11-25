using EHub.Domain.Enums;

namespace EHub.Domain.Entities;

// Stable category codes remain compatible with existing Showcase API contracts.
public sealed class ShowcaseViolationCategoryDefinition
{
    public ShowcaseViolationCategory Code { get; set; }
    public string NameVi { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string DescriptionVi { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public bool SupportsPhraseBlocking { get; set; }
    public int SortOrder { get; set; }
}

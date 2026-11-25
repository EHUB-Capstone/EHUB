namespace EHub.Contracts.Showcase;

public sealed class ShowcaseViolationCategoryDto
{
    public string Code { get; init; } = string.Empty;
    public string NameVi { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string DescriptionVi { get; init; } = string.Empty;
    public string DescriptionEn { get; init; } = string.Empty;
    public bool SupportsPhraseBlocking { get; init; }
    public int SortOrder { get; init; }
}

namespace EHub.Contracts.Showcase;

public sealed class ShowcaseViolationCategoryDto
{
    public string Code { get; init; } = string.Empty;
    public string NameVi { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

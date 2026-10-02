namespace EHub.Infrastructure.Options;

public sealed class DocumentPreviewOptions
{
    public const string SectionName = "DocumentPreview";

    public string LibreOfficeExecutablePath { get; set; } = "soffice";
    public int ConversionTimeoutSeconds { get; set; } = 60;
    public int MaximumConcurrentConversions { get; set; } = 2;
}

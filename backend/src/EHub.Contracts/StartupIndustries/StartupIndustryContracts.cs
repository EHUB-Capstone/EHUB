namespace EHub.Contracts.StartupIndustries;

public sealed class CreateStartupIndustryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = "active";
}

public sealed class UpdateStartupIndustryRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = "active";
}

public sealed class ChangeStartupIndustryStatusRequest
{
    public string Status { get; init; } = string.Empty;
}

public sealed class StartupIndustryResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class StartupIndustryListResponse
{
    public IReadOnlyCollection<StartupIndustryResponse> Industries { get; init; } = Array.Empty<StartupIndustryResponse>();
}

public sealed class StartupIndustryImportResponse
{
    public int ImportedCount { get; init; }
    public IReadOnlyCollection<StartupIndustryResponse> Industries { get; init; } = Array.Empty<StartupIndustryResponse>();
}

public sealed class StartupIndustryImportRowPreview
{
    public int RowNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsValid { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? ErrorMessage { get; init; }
}

public sealed class StartupIndustryImportPreviewResponse
{
    public int TotalRows { get; init; }
    public int ValidRowsCount { get; init; }
    public int ErrorRowsCount { get; init; }
    public IReadOnlyCollection<StartupIndustryImportRowPreview> Rows { get; init; } = Array.Empty<StartupIndustryImportRowPreview>();
}

namespace EHub.Contracts.StartupIndustries;

public sealed class CreateStartupIndustryRequest
{
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = "active";
}

public sealed class UpdateStartupIndustryRequest
{
    public string Name { get; init; } = string.Empty;
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
    public string Status { get; init; } = string.Empty;
}

public sealed class StartupIndustryListResponse
{
    public IReadOnlyCollection<StartupIndustryResponse> Industries { get; init; } = Array.Empty<StartupIndustryResponse>();
}

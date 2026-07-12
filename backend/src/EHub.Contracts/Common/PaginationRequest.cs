namespace EHub.Contracts.Common;

public class PaginationRequest
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Keyword { get; init; }
    public string? SortBy { get; init; }
    public bool IsDescending { get; init; }
}

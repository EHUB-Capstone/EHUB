using System.Collections.Generic;

namespace EHub.Contracts.Common;

public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; init; } = [];
    public int PageIndex { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }
    public int TotalPages { get; init; }
}

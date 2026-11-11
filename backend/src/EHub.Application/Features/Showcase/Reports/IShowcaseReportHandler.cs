using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Reports;

public interface IShowcaseReportHandler
{
    Task<Result<ShowcaseReportReceiptDto>> CreateAsync(Guid showcaseId, CreateShowcaseReportRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<PagedResponse<ShowcaseReportDto>>> ListAsync(string? status, int page, int pageSize, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result<ShowcaseReportDto>> ResolveAsync(Guid reportId, ResolveShowcaseReportRequest request, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteHistoryAsync(Guid reportId, Guid userId, string role, CancellationToken cancellationToken = default);
    Task<Result> DeleteAllHistoryAsync(string status, Guid userId, string role, CancellationToken cancellationToken = default);
}

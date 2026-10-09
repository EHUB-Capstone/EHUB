using EHub.Contracts.Dashboard;
using EHub.Shared.Results;

namespace EHub.Application.Features.Dashboard.GetSubmissionAnalytics;

public interface IGetSubmissionAnalyticsQueryHandler
{
    Task<Result<SubmissionAnalyticsResponse>> HandleAsync(
        Guid userId, IReadOnlyCollection<string> roles, GetSubmissionAnalyticsRequest request,
        CancellationToken cancellationToken = default);
}

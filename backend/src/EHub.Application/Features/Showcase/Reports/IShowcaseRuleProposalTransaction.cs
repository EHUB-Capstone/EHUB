using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Reports;

public interface IShowcaseRuleProposalTransaction
{
    Task<Result<T>> ExecuteAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation, CancellationToken token = default);
}

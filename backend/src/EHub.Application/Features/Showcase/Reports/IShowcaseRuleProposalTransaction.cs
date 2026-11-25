using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Reports;

public interface IShowcaseRuleProposalTransaction
{
    Task<Result<ShowcaseRuleProposalDto>> ExecuteAsync(
        Func<CancellationToken, Task<Result<ShowcaseRuleProposalDto>>> operation, CancellationToken token = default);
}

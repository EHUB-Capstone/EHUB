using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Showcase;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace EHub.Application.Features.Showcase.Reports;

public sealed class ShowcaseViolationCategoryQueryHandler(IApplicationDbContext context)
{
    public async Task<Result<IReadOnlyCollection<ShowcaseViolationCategoryDto>>> ListAsync(CancellationToken token = default)
    {
        var rows = await context.ShowcaseViolationCategories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new ShowcaseViolationCategoryDto { Code = x.Code.ToString(), NameVi = x.NameVi, NameEn = x.NameEn, SortOrder = x.SortOrder })
            .ToArrayAsync(token);
        return Result.Success<IReadOnlyCollection<ShowcaseViolationCategoryDto>>(rows);
    }
}

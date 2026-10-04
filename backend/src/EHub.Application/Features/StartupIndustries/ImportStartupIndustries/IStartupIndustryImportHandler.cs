using EHub.Contracts.StartupIndustries;
using EHub.Shared.Results;
using Microsoft.AspNetCore.Http;

namespace EHub.Application.Features.StartupIndustries.ImportStartupIndustries;

public interface IStartupIndustryImportHandler
{
    Task<Result<StartupIndustryImportPreviewResponse>> PreviewAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<Result<StartupIndustryImportResponse>> ImportAsync(
        IFormFile file,
        CancellationToken cancellationToken = default);
}

using EHub.Contracts.Showcase;
using EHub.Shared.Results;

namespace EHub.Application.Features.Showcase.Manage;

public interface IShowcaseManageHandler
{
    Task<Result<IReadOnlyCollection<MyShowcaseEntryDto>>> GetMineAsync(
        Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<ShowcaseDto>> CreateAsync(
        SaveShowcaseRequest request, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<ShowcaseDto>> UpdateAsync(
        Guid showcaseId, SaveShowcaseRequest request, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<ShowcaseMediaDto>> UploadMediaAsync(
        Guid showcaseId, Stream content, string fileName, long length, Guid userId, string role,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteMediaAsync(
        Guid showcaseId, Guid mediaId, Guid userId, string role, CancellationToken cancellationToken = default);

    Task<Result<ShowcaseDto>> UploadLogoAsync(
        Guid showcaseId, Stream content, string fileName, long length, Guid userId, string role,
        CancellationToken cancellationToken = default);
}

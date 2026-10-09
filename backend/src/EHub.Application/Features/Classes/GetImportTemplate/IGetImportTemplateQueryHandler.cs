using System.Threading;
using System.Threading.Tasks;
using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GetImportTemplate;

public interface IGetImportTemplateQueryHandler
{
    Task<Result<(byte[] FileBytes, string ContentType, string FileName)>> HandleAsync(
        CancellationToken cancellationToken = default);
}

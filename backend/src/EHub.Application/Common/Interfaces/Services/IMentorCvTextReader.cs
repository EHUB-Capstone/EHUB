using EHub.Shared.Results;

namespace EHub.Application.Common.Interfaces.Services;

public interface IMentorCvTextReader
{
    Task<Result<string>> ReadAsync(byte[] content, string extension, CancellationToken cancellationToken);
}

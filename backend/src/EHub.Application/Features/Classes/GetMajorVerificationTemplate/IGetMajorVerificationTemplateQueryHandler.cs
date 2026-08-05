using EHub.Shared.Results;

namespace EHub.Application.Features.Classes.GetMajorVerificationTemplate;

public interface IGetMajorVerificationTemplateQueryHandler
{
    Result<(byte[] FileBytes, string ContentType, string FileName)> Handle();
}

using EHub.Shared.Errors;

namespace EHub.Application.Common.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message = "Resource not found")
        : base(message, ErrorCodes.CommonNotFoundError)
    {
    }
}

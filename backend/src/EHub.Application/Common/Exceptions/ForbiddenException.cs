using EHub.Shared.Errors;

namespace EHub.Application.Common.Exceptions;

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message = "Forbidden")
        : base(message, ErrorCodes.CommonForbiddenError)
    {
    }
}

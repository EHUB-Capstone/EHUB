using EHub.Shared.Errors;

namespace EHub.Application.Common.Exceptions;

public sealed class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Unauthorized")
        : base(message, ErrorCodes.CommonUnauthorizedError)
    {
    }
}

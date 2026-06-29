using EHub.Shared.Errors;

namespace EHub.Application.Common.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(string message = "Resource conflict")
        : base(message, ErrorCodes.CommonConflictError)
    {
    }
}

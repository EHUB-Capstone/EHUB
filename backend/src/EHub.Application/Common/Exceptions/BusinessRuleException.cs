using EHub.Shared.Errors;

namespace EHub.Application.Common.Exceptions;

public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string message = "Business rule violation")
        : base(message, ErrorCodes.CommonBusinessRuleViolation)
    {
    }
}

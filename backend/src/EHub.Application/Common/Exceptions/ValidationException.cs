using System.Collections.Generic;
using System.Linq;
using EHub.Contracts.Common;
using EHub.Shared.Errors;

namespace EHub.Application.Common.Exceptions;

public sealed class ValidationException : AppException
{
    public ValidationException(IEnumerable<ValidationError> errors)
        : base("Validation failed", ErrorCodes.CommonValidationError)
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyCollection<ValidationError> Errors { get; }
}

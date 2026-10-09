using EHub.Shared.Errors;

namespace EHub.Shared.Errors;

public static class CommonErrors
{
    public static readonly Error Unauthorized = new(
        ErrorCodes.CommonUnauthorizedError,
        "Unauthorized access.");

    public static readonly Error Forbidden = new(
        ErrorCodes.CommonForbiddenError,
        "Forbidden access.");
}

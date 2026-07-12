using System.Collections.Generic;

namespace EHub.Contracts.Common;

public class ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Code { get; init; }
    public T? Data { get; init; }
    public IEnumerable<ValidationError>? Errors { get; init; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Success") => new()
    {
        Success = true,
        Message = message,
        Data = data
    };

    public static ApiResponse<T> FailureResponse(string message, string code, IEnumerable<ValidationError>? errors = null) => new()
    {
        Success = false,
        Message = message,
        Code = code,
        Errors = errors
    };
}

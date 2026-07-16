using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using EHub.Application.Common.Exceptions;
using EHub.Contracts.Common;
using EHub.Shared.Errors;

namespace EHub.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message, errors) = MapException(exception);

        LogException(exception, statusCode);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var response = ApiResponse<object>.FailureResponse(
            message: message,
            code: code,
            errors: errors);

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private (int StatusCode, string Code, string Message, IEnumerable<ValidationError>? Errors) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException validationException => (
                StatusCodes.Status400BadRequest,
                validationException.Code,
                validationException.Message,
                validationException.Errors),

            BusinessRuleException businessRuleException => (
                StatusCodes.Status400BadRequest,
                businessRuleException.Code,
                businessRuleException.Message,
                null),

            UnauthorizedException unauthorizedException => (
                StatusCodes.Status401Unauthorized,
                unauthorizedException.Code,
                unauthorizedException.Message,
                null),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                ErrorCodes.CommonUnauthorizedError,
                "Unauthorized access",
                null),

            ForbiddenException forbiddenException => (
                StatusCodes.Status403Forbidden,
                forbiddenException.Code,
                forbiddenException.Message,
                null),

            NotFoundException notFoundException => (
                StatusCodes.Status404NotFound,
                notFoundException.Code,
                notFoundException.Message,
                null),

            ConflictException conflictException => (
                StatusCodes.Status409Conflict,
                conflictException.Code,
                conflictException.Message,
                null),

            AppException appException => (
                StatusCodes.Status400BadRequest,
                appException.Code,
                appException.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalServerError,
                _environment.IsDevelopment() || _environment.IsEnvironment("Testing")
                    ? $"{exception.GetType().Name}: {exception.Message}" + 
                      (exception.InnerException != null 
                          ? $" | INNER: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}" 
                          : "")
                    : "An unexpected error occurred.",
                null)
        };
    }

    private void LogException(Exception exception, int statusCode)
    {
        if (statusCode >= (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception occurred.");
            return;
        }

        _logger.LogWarning(
            exception,
            "Handled application exception occurred with status code {StatusCode}.",
            statusCode);
    }
}

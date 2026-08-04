using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using EHub.Api.Middlewares;
using EHub.Contracts.Common;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EHub.IntegrationTests.Middlewares;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task UnhandledException_DoesNotExposeExceptionMessage()
    {
        const string sensitiveMessage = "database connection details must not reach clients";
        var context = new DefaultHttpContext();
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(sensitiveMessage),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        responseBody.Position = 0;
        using var reader = new StreamReader(responseBody);
        var json = await reader.ReadToEndAsync();
        var response = JsonSerializer.Deserialize<ApiResponse<object>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        json.Should().NotContain(sensitiveMessage);
        response.Should().NotBeNull();
        response!.Code.Should().Be(ErrorCodes.InternalServerError);
        response.Message.Should().Be("An unexpected error occurred.");
    }
}

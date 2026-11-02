using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Showcase;
using EHub.Domain.Entities;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Showcase;

[Collection("Sequential")]
public sealed class ShowcaseAuthorizationIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public ShowcaseAuthorizationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("GET", "/api/showcase/mine")]
    [InlineData("POST", "/api/showcase")]
    [InlineData("PUT", "/api/showcase/00000000-0000-0000-0000-000000000001")]
    [InlineData("DELETE", "/api/showcase/00000000-0000-0000-0000-000000000001/media/00000000-0000-0000-0000-000000000002")]
    [InlineData("GET", "/api/showcase/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/submit")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/withdraw")]
    [InlineData("GET", "/api/showcase/moderation")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/request-changes")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/reject")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/hide")]
    [InlineData("POST", "/api/showcase/00000000-0000-0000-0000-000000000001/unhide")]
    public async Task ManageEndpoints_RequireAuthentication(string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(Payload(Guid.NewGuid()));

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(SystemRoles.Lecturer, "GET", "/api/showcase/mine")]
    [InlineData(SystemRoles.Admin, "GET", "/api/showcase/mine")]
    [InlineData(SystemRoles.Mentor, "POST", "/api/showcase")]
    [InlineData(SystemRoles.Lecturer, "PUT", "/api/showcase/00000000-0000-0000-0000-000000000001")]
    [InlineData(SystemRoles.Lecturer, "POST", "/api/showcase/00000000-0000-0000-0000-000000000001/submit")]
    [InlineData(SystemRoles.Admin, "POST", "/api/showcase/00000000-0000-0000-0000-000000000001/withdraw")]
    [InlineData(SystemRoles.Student, "GET", "/api/showcase/moderation")]
    [InlineData(SystemRoles.Student, "POST", "/api/showcase/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData(SystemRoles.Student, "POST", "/api/showcase/00000000-0000-0000-0000-000000000001/hide")]
    [InlineData(SystemRoles.Mentor, "GET", "/api/showcase/moderation")]
    [InlineData(SystemRoles.Mentor, "POST", "/api/showcase/00000000-0000-0000-0000-000000000001/reject")]
    public async Task ManageEndpoints_AreForbiddenForNonStudents(string role, string method, string path)
    {
        using var client = _factory.CreateClient();
        using var request = AuthorizedRequest(new HttpMethod(method), path, role,
            method is "POST" or "PUT" ? Payload(Guid.NewGuid()) : null);

        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_ForALineageTheStudentDoesNotOwn_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();
        using var request = AuthorizedRequest(HttpMethod.Post, "/api/showcase", SystemRoles.Student, Payload(Guid.NewGuid()));

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body!.Code.Should().Be(ErrorCodes.ShowcaseNotFound);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        var invalid = new SaveShowcaseRequest { ProjectLineageId = Guid.NewGuid(), DevelopmentStage = "Unicorn" };
        using var request = AuthorizedRequest(HttpMethod.Post, "/api/showcase", SystemRoles.Student, invalid);

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body!.Code.Should().Be(ErrorCodes.ShowcaseValidationError);
    }

    private static SaveShowcaseRequest Payload(Guid lineageId) => new()
    {
        ProjectLineageId = lineageId,
        DevelopmentStage = "Mvp"
    };

    private HttpRequestMessage AuthorizedRequest(HttpMethod method, string path, string role, object? payload)
    {
        using var scope = _factory.Services.CreateScope();
        var user = new User
        {
            FullName = $"{role} showcase tester",
            Email = $"showcase-{role.ToLowerInvariant()}@example.test"
        };
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [role]).Token;
        var request = new HttpRequestMessage(method, path);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

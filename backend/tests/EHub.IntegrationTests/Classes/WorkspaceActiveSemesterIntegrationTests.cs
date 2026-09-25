using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Subjects;
using EHub.Domain.Entities;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Classes;

[Collection("Sequential")]
public sealed class WorkspaceActiveSemesterIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public WorkspaceActiveSemesterIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetWorkspaceActiveSemester_RequiresAuthenticationAndStaffOrMentorRole()
    {
        using var client = _factory.CreateClient();
        using var anonymousResponse = await client.GetAsync("/api/workspace/active-semester");
        anonymousResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var studentRequest = AuthorizedRequest(SystemRoles.Student);
        using var studentResponse = await client.SendAsync(studentRequest);
        studentResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(SystemRoles.Admin)]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Mentor)]
    public async Task GetWorkspaceActiveSemester_ReturnsCurrentSemesterForWorkspaceRoles(string role)
    {
        using var client = _factory.CreateClient();
        using var request = AuthorizedRequest(role);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<CurrentSemesterResponse>>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }

    private HttpRequestMessage AuthorizedRequest(string role)
    {
        using var scope = _factory.Services.CreateScope();
        var user = new User { FullName = "Workspace reader", Email = "workspace-reader@example.test" };
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .GenerateAccessToken(user, [role]).Token;
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspace/active-semester");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

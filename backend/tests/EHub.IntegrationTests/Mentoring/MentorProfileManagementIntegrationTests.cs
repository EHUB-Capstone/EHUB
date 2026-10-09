using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Contracts.Common;
using EHub.Contracts.Mentoring;
using EHub.Domain.Entities;
using EHub.IntegrationTests.Common;
using EHub.Infrastructure.Persistence;
using EHub.Shared.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Mentoring;

[Collection("Sequential")]
public sealed class MentorProfileManagementIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();
    private static SaveAdminMentorProfileRequest Request(string email, string area = "AI") => new()
    {
        FullName = "Test Mentor", Email = email, TemporaryPassword = "Test@123456",
        Profile = new UpdateMentorProfileRequest { MentorType = "IT", Expertise = ["Software"], Bio = "Technology advisor",
            StartupDomains = ["Education"], TechnologySkills = ["Cloud"], Tags = ["Founder"],
            Experiences = [new MentorExperienceDto { Kind = "Technology", Area = area, Years = 4, Level = "Advanced" }] }
    };

    [Fact]
    public async Task ManagementEndpointsRequireAuthentication()
    {
        (await client.PostAsJsonAsync("/api/mentoring/profiles", Request("demo@example.test"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await client.PutAsJsonAsync($"/api/mentoring/profiles/{Guid.NewGuid()}", Request("demo@example.test"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(SystemRoles.Lecturer)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData(SystemRoles.Student)]
    public async Task NonAdminCannotCreateOrUpdate(string role)
    {
        var token = await Token(role);
        using var create = Authorized(HttpMethod.Post, "/api/mentoring/profiles", Request("demo@example.test"), token);
        (await client.SendAsync(create)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var update = Authorized(HttpMethod.Put, $"/api/mentoring/profiles/{Guid.NewGuid()}", Request("demo@example.test"), token);
        (await client.SendAsync(update)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminCanCreateUpdateAndSearchStructuredDataWithDuplicateValidation()
    {
        var token = await Token(SystemRoles.Admin);
        var email = $"profile-{Guid.NewGuid():N}@example.test";
        using var create = Authorized(HttpMethod.Post, "/api/mentoring/profiles", Request(email), token);
        var response = await client.SendAsync(create);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = (await response.Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse>>())!.Data!;
        created.Experiences.Should().ContainSingle().Which.Area.Should().Be("AI");
        using var update = Authorized(HttpMethod.Put, $"/api/mentoring/profiles/{created.Id}", Request(email, "Cloud"), token);
        (await client.SendAsync(update)).StatusCode.Should().Be(HttpStatusCode.OK);
        using var directory = new HttpRequestMessage(HttpMethod.Get, "/api/mentoring/directory");
        directory.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listed = await (await client.SendAsync(directory)).Content.ReadFromJsonAsync<ApiResponse<MentorProfileResponse[]>>();
        listed!.Data!.Single(x => x.Id == created.Id).Experiences.Should().ContainSingle().Which.Area.Should().Be("Cloud");
        using var duplicate = Authorized(HttpMethod.Post, "/api/mentoring/profiles", Request(email), token);
        (await client.SendAsync(duplicate)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var invalid = Authorized(HttpMethod.Put, $"/api/mentoring/profiles/{created.Id}", new SaveAdminMentorProfileRequest
        { FullName = "Test", Email = email, Profile = new UpdateMentorProfileRequest { MentorType = "IT", Expertise = ["AI", " ai "], Bio = "Advisor" } }, token);
        (await client.SendAsync(invalid)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var missing = Authorized(HttpMethod.Put, $"/api/mentoring/profiles/{Guid.NewGuid()}", Request(email), token);
        (await client.SendAsync(missing)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<string> Token(string roleName)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var role = await db.Roles.SingleAsync(x => x.Name == roleName);
        var email = $"role-{Guid.NewGuid():N}@example.test";
        var user = new User { FullName = "Profile Test User", Email = email, NormalizedEmail = email, PasswordHash = "unused" };
        user.UserRoles.Add(new UserRole { User = user, UserId = user.Id, Role = role, RoleId = role.Id });
        db.Users.Add(user); await db.SaveChangesAsync();
        return scope.ServiceProvider.GetRequiredService<IJwtTokenService>().GenerateAccessToken(user, [roleName]).Token;
    }
    private static HttpRequestMessage Authorized<T>(HttpMethod method, string url, T body, string token)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

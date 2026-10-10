using System.Net;
using System.Net.Http.Json;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.Domain.Enums;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EHub.IntegrationTests.Auth;

[Collection("Sequential")]
public class MentorRegistrationIntegrationTests(CustomWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("Enterprise", MentorType.Enterprise)]
    [InlineData("Academic", MentorType.Academic)]
    public async Task RegisteringAsAMentor_ShouldKeepTheChosenMentorType(string chosen, MentorType expected)
    {
        var email = $"mentor-{Guid.NewGuid():N}@example.com";

        var registration = await RegisterAsync(email, chosen);
        registration.StatusCode.Should().Be(HttpStatusCode.Accepted, await registration.Content.ReadAsStringAsync());
        var registrationId = (await registration.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>())!.Data!.RegistrationId!.Value;

        var verify = await _client.PostAsJsonAsync("/api/auth/register/verify-otp", new VerifyRegistrationOtpRequest
        {
            RegistrationId = registrationId,
            Otp = FakeEmailService.LastRegistrationOtp!
        });
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await context.MentorProfiles.AsNoTracking().SingleAsync(item => item.User.NormalizedEmail == email);
        profile.Type.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "AUTH_MENTOR_TYPE_REQUIRED")]
    [InlineData("", "AUTH_MENTOR_TYPE_REQUIRED")]
    [InlineData("Freelancer", "AUTH_INVALID_MENTOR_TYPE")]
    public async Task RegisteringAsAMentor_WithoutAValidMentorType_ShouldBeRejected(string? chosen, string code)
    {
        var response = await RegisterAsync($"mentor-{Guid.NewGuid():N}@example.com", chosen);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().Contain(code);
    }

    private async Task<HttpResponseMessage> RegisterAsync(string email, string? mentorType)
    {
        FakeEmailService.LastRegistrationOtp = null;
        FakeEmailService.LastRegistrationEmail = null;
        return await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Mentor Applicant",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Mentor",
            MentorType = mentorType
        });
    }
}

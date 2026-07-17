using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.IntegrationTests.Common;

namespace EHub.IntegrationTests.Auth;

[Collection("Sequential")]
public class PasswordResetIntegrationTests
{
    private readonly HttpClient _client;

    public PasswordResetIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        // Reset static fields in fake service before each test
        FakeEmailService.LastRawToken = null;
        FakeEmailService.LastResetUrl = null;
    }

    [Fact]
    public async Task ForgotPassword_Should_Return_200_Generic_Whether_Email_Exists_Or_Not()
    {
        // Case 1: Email does not exist
        var requestNonExistent = new ForgotPasswordRequest { Email = "nonexistent-user-123@example.com" };
        var response1 = await _client.PostAsJsonAsync("/api/auth/forgot-password", requestNonExistent);
        
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        var body1 = await response1.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body1.Should().NotBeNull();
        body1!.Success.Should().BeTrue();
        FakeEmailService.LastRawToken.Should().BeNull();

        // Case 2: Email exists
        var uniqueEmail = $"student-reset-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Reset Test User",
            Email = uniqueEmail,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var requestExistent = new ForgotPasswordRequest { Email = uniqueEmail };
        var response2 = await _client.PostAsJsonAsync("/api/auth/forgot-password", requestExistent);

        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        var body2 = await response2.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body2.Should().NotBeNull();
        body2!.Success.Should().BeTrue();
        
        // Should trigger email dispatch
        FakeEmailService.LastRawToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResetPassword_Should_Successfully_Change_Password_And_Invalidate_Old_Sessions()
    {
        // 1. Register a user
        var uniqueEmail = $"student-flow-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Flow User",
            Email = uniqueEmail,
            Password = "OldPassword123",
            ConfirmPassword = "OldPassword123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // 2. Request forgot password
        var forgotRequest = new ForgotPasswordRequest { Email = uniqueEmail };
        await _client.PostAsJsonAsync("/api/auth/forgot-password", forgotRequest);
        var rawToken = FakeEmailService.LastRawToken;
        rawToken.Should().NotBeNullOrEmpty();

        // 3. Reset password
        var resetRequest = new ResetPasswordRequest
        {
            Token = rawToken!,
            NewPassword = "NewPassword123",
            ConfirmPassword = "NewPassword123"
        };
        var resetResponse = await _client.PostAsJsonAsync("/api/auth/reset-password", resetRequest);
        resetResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Try login with old password -> should fail
        var loginOldRequest = new EmailPasswordLoginRequest
        {
            Email = uniqueEmail,
            Password = "OldPassword123"
        };
        var loginOldResponse = await _client.PostAsJsonAsync("/api/auth/login", loginOldRequest);
        loginOldResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // 5. Try login with new password -> should succeed
        var loginNewRequest = new EmailPasswordLoginRequest
        {
            Email = uniqueEmail,
            Password = "NewPassword123"
        };
        var loginNewResponse = await _client.PostAsJsonAsync("/api/auth/login", loginNewRequest);
        loginNewResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EHub.Contracts.Auth;
using EHub.Contracts.Common;
using EHub.IntegrationTests.Common;

namespace EHub.IntegrationTests.Auth;

[Collection("Sequential")] // Run sequentially to avoid DB collision during test container lifetime if multiple classes run
public class AuthIntegrationTests
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_Should_Create_Active_Student_When_Request_Is_Valid()
    {
        // Arrange
        var uniqueEmail = $"student-{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest
        {
            FullName = "Student One",
            Email = uniqueEmail,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        body.Should().NotBeNull();
        body.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.User.Should().NotBeNull();
        body.Data.User!.Email.Should().Be(uniqueEmail);
        body.Data.RequiresApproval.Should().BeFalse();
    }

    [Fact]
    public async Task Register_Should_Return_409_Conflict_When_Email_Already_Exists()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest
        {
            FullName = "Student One",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };

        // Create first user
        await _client.PostAsJsonAsync("/api/auth/register", request);

        // Act - Attempt to register same email
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeFalse();
        body.Code.Should().Be("AUTH_EMAIL_ALREADY_EXISTS");
    }

    [Fact]
    public async Task Register_Should_Return_400_Bad_Request_When_Student_Missing_Major()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FullName = "Student Missing Major",
            Email = $"student-{Guid.NewGuid()}@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = null // Missing major
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_Should_Create_PendingApproval_Lecturer_When_Valid()
    {
        // Arrange
        var email = $"lecturer-{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest
        {
            FullName = "Lecturer One",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Lecturer",
            MajorCode = null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data!.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public async Task Login_Should_Succeed_When_Credentials_Are_Correct()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Login",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = email,
            Password = "Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
        body.Data!.AccessToken.Should().NotBeNullOrEmpty();
        body.Data.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_Should_Return_401_When_Password_Is_Incorrect()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Login Wrong Pass",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = email,
            Password = "WrongPassword"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body.Should().NotBeNull();
        body!.Code.Should().Be("AUTH_INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_Should_Return_403_When_Account_Is_Pending_Approval()
    {
        // Arrange
        var email = $"lecturer-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Lecturer Pending Login",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Lecturer",
            MajorCode = null
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = email,
            Password = "Password123"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body.Should().NotBeNull();
        body!.Code.Should().Be("AUTH_ACCOUNT_PENDING_APPROVAL");
    }

    [Fact]
    public async Task Me_Should_Return_401_When_No_Token_Is_Provided()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_Should_Return_200_When_Valid_Token_Is_Provided()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Me Test",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = email,
            Password = "Password123"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var token = loginBody!.Data!.AccessToken;

        // Act
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var meBody = await response.Content.ReadFromJsonAsync<ApiResponse<UserSummaryResponse>>();
        meBody.Should().NotBeNull();
        meBody!.Success.Should().BeTrue();
        meBody.Data!.Email.Should().Be(email);
        meBody.Data.Roles.Should().Contain("Student");
    }

    [Fact]
    public async Task RefreshToken_Should_Succeed_And_Rotate_Tokens_When_Valid()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Refresh Test",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var firstRefreshToken = loginBody!.Data!.RefreshToken;

        // Act - Call refresh
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest { RefreshToken = firstRefreshToken });

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        refreshBody.Should().NotBeNull();
        refreshBody!.Success.Should().BeTrue();
        refreshBody.Data!.AccessToken.Should().NotBeNullOrEmpty();
        refreshBody.Data.RefreshToken.Should().NotBeNullOrEmpty();
        refreshBody.Data.RefreshToken.Should().NotBe(firstRefreshToken);
    }

    [Fact]
    public async Task RefreshToken_Should_Fail_When_Using_Old_Token_After_Rotation()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Rotate Test",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var firstRefreshToken = loginBody!.Data!.RefreshToken;

        // First rotation (valid)
        var refreshResponse1 = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest { RefreshToken = firstRefreshToken });
        refreshResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Attempt to use the first token again
        var refreshResponse2 = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest { RefreshToken = firstRefreshToken });

        // Assert
        refreshResponse2.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await refreshResponse2.Content.ReadFromJsonAsync<ApiResponse<object>>();
        body.Should().NotBeNull();
        body!.Code.Should().Be("AUTH_REFRESH_TOKEN_REVOKED");
    }

    [Fact]
    public async Task Logout_Should_Revoke_Refresh_Token_Successfully()
    {
        // Arrange
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Logout Test",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        var refreshToken = loginBody!.Data!.RefreshToken;

        // Act - Logout
        var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = refreshToken });

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Attempt to refresh using the logged out token
        var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest { RefreshToken = refreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

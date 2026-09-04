using System;
using System.Linq;
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

[Collection("Sequential")]
public class AuthIntegrationTests
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private string ExtractRefreshToken(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            var cookie = values.FirstOrDefault(v => v.StartsWith("ehub_refresh_token="));
            if (cookie != null)
            {
                var parts = cookie.Split(';');
                var firstPart = parts[0];
                return firstPart.Substring("ehub_refresh_token=".Length);
            }
        }
        return string.Empty;
    }

    private async Task<(HttpResponseMessage Response, RegisterResponse Body)> RegisterAndVerifyAsync(
        RegisterRequest request)
    {
        FakeEmailService.LastRegistrationOtp = null;
        FakeEmailService.LastRegistrationEmail = null;

        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", request);
        registerResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var registerBody = await registerResponse.Content
            .ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        registerBody.Should().NotBeNull();
        registerBody!.Data.Should().NotBeNull();
        registerBody.Data!.RequiresEmailVerification.Should().BeTrue();
        registerBody.Data.RegistrationId.Should().NotBeNull();
        FakeEmailService.LastRegistrationOtp.Should().MatchRegex("^[0-9]{6}$");
        FakeEmailService.LastRegistrationEmail.Should().Be(request.Email.ToLowerInvariant());

        var verifyResponse = await _client.PostAsJsonAsync(
            "/api/auth/register/verify-otp",
            new VerifyRegistrationOtpRequest
            {
                RegistrationId = registerBody.Data.RegistrationId!.Value,
                Otp = FakeEmailService.LastRegistrationOtp!
            });
        var verifyBody = await verifyResponse.Content
            .ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        verifyBody.Should().NotBeNull();
        verifyBody!.Data.Should().NotBeNull();

        return (verifyResponse, verifyBody.Data!);
    }

    [Fact]
    public async Task RegisterAndVerify_Should_Create_Active_Student_When_Request_Is_Valid()
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
        var (response, body) = await RegisterAndVerifyAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.User.Should().NotBeNull();
        body.User!.Email.Should().Be(uniqueEmail);
        body.RequiresEmailVerification.Should().BeFalse();
        body.RequiresApproval.Should().BeFalse();

        // Should also set the refresh token cookie
        var refreshToken = ExtractRefreshToken(response);
        refreshToken.Should().NotBeNullOrEmpty();
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
        await RegisterAndVerifyAsync(request);

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
    public async Task Register_Should_Not_Create_LoginAccount_BeforeOtpVerification()
    {
        var email = $"unverified-{Guid.NewGuid()}@example.com";
        var response = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Unverified Student",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        ExtractRefreshToken(response).Should().BeEmpty();

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyOtp_Should_RejectWrongCode_ThenAcceptDeliveredCode()
    {
        FakeEmailService.LastRegistrationOtp = null;
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            FullName = "Otp Attempt Student",
            Email = $"otp-{Guid.NewGuid()}@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        });
        var registerBody = await registerResponse.Content
            .ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        var registrationId = registerBody!.Data!.RegistrationId!.Value;
        var deliveredOtp = FakeEmailService.LastRegistrationOtp!;
        var wrongOtp = deliveredOtp == "000000" ? "000001" : "000000";

        var wrongResponse = await _client.PostAsJsonAsync(
            "/api/auth/register/verify-otp",
            new VerifyRegistrationOtpRequest { RegistrationId = registrationId, Otp = wrongOtp });
        wrongResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var wrongBody = await wrongResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        wrongBody!.Code.Should().Be("AUTH_VERIFICATION_CODE_INVALID");

        var correctResponse = await _client.PostAsJsonAsync(
            "/api/auth/register/verify-otp",
            new VerifyRegistrationOtpRequest { RegistrationId = registrationId, Otp = deliveredOtp });
        correctResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        ExtractRefreshToken(correctResponse).Should().NotBeEmpty();
    }

    [Fact]
    public async Task RegisterAndVerify_Should_Create_PendingApproval_Lecturer_When_Valid()
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
        var (response, body) = await RegisterAndVerifyAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RequiresEmailVerification.Should().BeFalse();
        body.RequiresApproval.Should().BeTrue();
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
        await RegisterAndVerifyAsync(registerRequest);

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

        var refreshToken = ExtractRefreshToken(response);
        refreshToken.Should().NotBeNullOrEmpty();
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
        await RegisterAndVerifyAsync(registerRequest);

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
        await RegisterAndVerifyAsync(registerRequest);

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
            FullName = "Student Token Test",
            Email = email,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Student",
            MajorCode = "BIT_SE"
        };
        await RegisterAndVerifyAsync(registerRequest);

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
        await RegisterAndVerifyAsync(registerRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        var firstRefreshToken = ExtractRefreshToken(loginResponse);

        // Act - Call refresh using Cookie
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
        refreshRequest.Headers.Add("Cookie", $"ehub_refresh_token={firstRefreshToken}");
        var refreshResponse = await _client.SendAsync(refreshRequest);

        // Assert
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        refreshBody.Should().NotBeNull();
        refreshBody!.Success.Should().BeTrue();
        refreshBody.Data!.AccessToken.Should().NotBeNullOrEmpty();

        var newRefreshToken = ExtractRefreshToken(refreshResponse);
        newRefreshToken.Should().NotBeNullOrEmpty();
        newRefreshToken.Should().NotBe(firstRefreshToken);
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
        await RegisterAndVerifyAsync(registerRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        var firstRefreshToken = ExtractRefreshToken(loginResponse);

        // First rotation (valid)
        var refreshRequest1 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
        refreshRequest1.Headers.Add("Cookie", $"ehub_refresh_token={firstRefreshToken}");
        var refreshResponse1 = await _client.SendAsync(refreshRequest1);
        refreshResponse1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Attempt to use the first token again
        var refreshRequest2 = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
        refreshRequest2.Headers.Add("Cookie", $"ehub_refresh_token={firstRefreshToken}");
        var refreshResponse2 = await _client.SendAsync(refreshRequest2);

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
        await RegisterAndVerifyAsync(registerRequest);

        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", new EmailPasswordLoginRequest { Email = email, Password = "Password123" });
        var refreshToken = ExtractRefreshToken(loginResponse);

        // Act - Logout using Cookie
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"ehub_refresh_token={refreshToken}");
        var logoutResponse = await _client.SendAsync(logoutRequest);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Attempt to refresh using the logged out token
        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh-token");
        refreshRequest.Headers.Add("Cookie", $"ehub_refresh_token={refreshToken}");
        var refreshResponse = await _client.SendAsync(refreshRequest);
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

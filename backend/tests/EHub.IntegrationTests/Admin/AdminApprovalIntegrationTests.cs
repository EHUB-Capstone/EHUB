using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using EHub.Contracts.Auth;
using EHub.Contracts.Admin.Users;
using EHub.Contracts.Common;
using EHub.IntegrationTests.Common;

namespace EHub.IntegrationTests.Admin;

[Collection("Sequential")]
public class AdminApprovalIntegrationTests
{
    private readonly HttpClient _client;

    public AdminApprovalIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = "admin@ehub.test",
            Password = "Admin@123456"
        };
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    private async Task<string> GetStudentTokenAsync()
    {
        var email = $"student-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Student Test User",
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
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        return body!.Data!.AccessToken;
    }

    [Fact]
    public async Task PendingList_Should_Return_401_When_No_Token_Is_Provided()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/users/pending-approval");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PendingList_Should_Return_403_When_Student_Token_Is_Provided()
    {
        // Arrange
        var studentToken = await GetStudentTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users/pending-approval");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", studentToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PendingList_Should_Return_200_When_Admin_Token_Is_Provided()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users/pending-approval");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<PendingApprovalUserResponse>>>();
        body.Should().NotBeNull();
        body!.Success.Should().BeTrue();
        body.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Admin_Approve_Lecturer_Should_Enable_Lecturer_Login()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();

        // 1. Register Lecturer (status is PendingApproval)
        var lecturerEmail = $"lecturer-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Lecturer Approved Test",
            Email = lecturerEmail,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Lecturer",
            MajorCode = null
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        var lecturerId = registerBody!.Data!.User!.Id;

        // 2. Fetch pending list, verify Lecturer is in list
        var pendingRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users/pending-approval");
        pendingRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var pendingResponse = await _client.SendAsync(pendingRequest);
        var pendingBody = await pendingResponse.Content.ReadFromJsonAsync<ApiResponse<List<PendingApprovalUserResponse>>>();
        pendingBody!.Data.Should().Contain(u => u.Id == lecturerId);

        // 3. Approve the lecturer
        var approveRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{lecturerId}/approve");
        approveRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var approveResponse = await _client.SendAsync(approveRequest);
        approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 4. Verify Lecturer login now succeeds
        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = lecturerEmail,
            Password = "Password123"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>();
        loginBody.Should().NotBeNull();
        loginBody!.Success.Should().BeTrue();
        loginBody.Data!.User.Email.Should().Be(lecturerEmail);
        loginBody.Data.User.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Admin_Reject_Mentor_Should_Block_Mentor_Login()
    {
        // Arrange
        var adminToken = await GetAdminTokenAsync();

        // 1. Register Mentor (status is PendingApproval)
        var mentorEmail = $"mentor-{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            FullName = "Mentor Rejected Test",
            Email = mentorEmail,
            Password = "Password123",
            ConfirmPassword = "Password123",
            Role = "Mentor",
            MajorCode = null
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        var mentorId = registerBody!.Data!.User!.Id;

        // 2. Reject the mentor
        var rejectRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/users/{mentorId}/reject");
        rejectRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var rejectResponse = await _client.SendAsync(rejectRequest);
        rejectResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // 3. Verify Mentor login returns 403 Forbidden
        var loginRequest = new EmailPasswordLoginRequest
        {
            Email = mentorEmail,
            Password = "Password123"
        };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<object>>();
        loginBody.Should().NotBeNull();
        loginBody!.Code.Should().Be("AUTH_ACCOUNT_REJECTED");
    }
}

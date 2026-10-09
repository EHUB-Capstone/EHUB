using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.GoogleLogin;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using EHub.Application.Common.Models.Identity;
using EHub.Application.Features.Auth;
using EHub.Domain.Common;
using EHub.Shared.Errors;

using Microsoft.Extensions.Logging;

namespace EHub.ApplicationTests.Features.Auth.GoogleLogin;

public class GoogleLoginCommandHandlerTests
{
    private readonly IGoogleAuthService _googleAuthService = Substitute.For<IGoogleAuthService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();
    private readonly IRoleRepository _roleRepository = Substitute.For<IRoleRepository>();
    private readonly IUserRoleRepository _userRoleRepository = Substitute.For<IUserRoleRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();
    private readonly ILogger<GoogleLoginCommandHandler> _logger = Substitute.For<ILogger<GoogleLoginCommandHandler>>();

    private readonly GoogleLoginCommandHandler _handler;

    public GoogleLoginCommandHandlerTests()
    {
        _handler = new GoogleLoginCommandHandler(
            _googleAuthService,
            _userRepository,
            _studentRepository,
            _refreshTokenRepository,
            _unitOfWork,
            _jwtTokenService,
            _refreshTokenService,
            _logger,
            _roleRepository,
            _userRoleRepository,
            Substitute.For<EHub.Application.Common.Interfaces.Services.IDateTimeProvider>());
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Login_Successfully_When_Google_Token_Valid_And_User_Active()
    {
        var request = new GoogleLoginRequest { IdToken = "valid-google-id-token" };

        var googleUserInfo = new GoogleUserInfo
        {
            Email = "student@fpt.edu.vn",
            FullName = "Nguyen Van A",
            EmailVerified = true,
            Subject = "google-sub-id"
        };

        _googleAuthService.VerifyIdTokenAsync("valid-google-id-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success(googleUserInfo));

        var userId = Guid.NewGuid();
        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            NormalizedEmail = "student@fpt.edu.vn",
            Status = UserStatus.Active
        };
        SetId(user, userId);

        var role = new Role { Name = SystemRoles.Student };
        SetId(role, Guid.NewGuid());
        user.UserRoles.Add(new UserRole { UserId = userId, Role = role });

        _userRepository.GetByEmailWithRolesAsync("student@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        
        _jwtTokenService.GenerateAccessToken(user, Arg.Any<string[]>()).Returns(new AccessTokenResult
        {
            Token = "access_token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });

        _refreshTokenService.GenerateRefreshToken().Returns(new RefreshTokenResult
        {
            RawToken = "raw_refresh_token",
            TokenHash = "token_hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });

        _studentRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new Student
        {
            UserId = userId,
            FullName = "Imported Student Name",
            MajorCode = MajorCodes.BIT_SE
        });

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access_token", result.Value.AccessToken);
        Assert.Equal("raw_refresh_token", result.Value.RefreshToken);
        Assert.Equal(UserStatus.Active.ToString(), result.Value.User?.Status);
        Assert.Equal("Imported Student Name", result.Value.User?.FullName);
        Assert.Equal(MajorCodes.BIT_SE, result.Value.User?.MajorCode);
        _userRepository.Received(1).Update(Arg.Is<User>(candidate => candidate != null && candidate.FullName == "Imported Student Name"));

        await _refreshTokenRepository.Received(1).AddAsync(Arg.Any<EHub.Domain.Entities.RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_Login_When_Google_Token_Invalid()
    {
        var request = new GoogleLoginRequest { IdToken = "invalid-google-id-token" };

        var invalidError = new Error(ErrorCodes.AuthInvalidGoogleToken, "Invalid Google token.");
        _googleAuthService.VerifyIdTokenAsync("invalid-google-id-token", Arg.Any<CancellationToken>())
            .Returns(Result.Failure<GoogleUserInfo>(invalidError));

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthInvalidGoogleToken, result.Error.Code);

        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailWithRolesAsync(default!, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_Login_When_Google_Email_Not_Verified()
    {
        var request = new GoogleLoginRequest { IdToken = "unverified-google-id-token" };

        var googleUserInfo = new GoogleUserInfo
        {
            Email = "student@fpt.edu.vn",
            FullName = "Nguyen Van A",
            EmailVerified = false,
            Subject = "google-sub-id"
        };

        _googleAuthService.VerifyIdTokenAsync("unverified-google-id-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success(googleUserInfo));

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AuthGoogleEmailNotVerified, result.Error.Code);

        await _userRepository.DidNotReceiveWithAnyArgs().GetByEmailWithRolesAsync(default!, default!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Should_Create_Student_And_Login_When_User_Not_Registered(bool hasRosterProfile)
    {
        var request = new GoogleLoginRequest { IdToken = "valid-google-id-token" };

        var googleUserInfo = new GoogleUserInfo
        {
            Email = "notregistered@fpt.edu.vn",
            FullName = "Unregistered User",
            EmailVerified = true,
            Subject = "google-sub-id"
        };

        _googleAuthService.VerifyIdTokenAsync("valid-google-id-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success(googleUserInfo));

        _userRepository.GetByEmailWithRolesAsync("notregistered@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns((User?)null);
        _roleRepository.GetByNameAsync(SystemRoles.Student, Arg.Any<CancellationToken>())
            .Returns(new Role { Name = SystemRoles.Student });
        _jwtTokenService.GenerateAccessToken(Arg.Any<User>(), Arg.Any<string[]>())
            .Returns(new AccessTokenResult { Token = "access", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        _refreshTokenService.GenerateRefreshToken().Returns(new RefreshTokenResult
        {
            RawToken = "refresh", TokenHash = "hash", ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        var rosterStudent = new Student { Email = googleUserInfo.Email, FullName = "Imported Student Name", MajorCode = MajorCodes.BIT_SE };
        if (hasRosterProfile)
        {
            _studentRepository.GetUnlinkedByEmailAsync(googleUserInfo.Email, Arg.Any<CancellationToken>()).Returns(rosterStudent);
            _studentRepository.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(rosterStudent);
        }

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { SystemRoles.Student }, result.Value.User.Roles);
        Assert.Equal(hasRosterProfile ? "Imported Student Name" : googleUserInfo.FullName, result.Value.User.FullName);
        Assert.Equal(hasRosterProfile ? MajorCodes.BIT_SE : null, result.Value.User.MajorCode);
        await _userRepository.Received(1).AddAsync(Arg.Is<User>(u => u != null && u.IsEmailVerified && u.Status == UserStatus.Active), Arg.Any<CancellationToken>());
        if (hasRosterProfile)
        {
            Assert.Equal(result.Value.User.Id, rosterStudent.UserId);
            _studentRepository.Received(1).Update(rosterStudent);
            await _studentRepository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
        }
        else
        {
            await _studentRepository.Received(1).AddAsync(Arg.Is<Student>(s => s != null && s.MajorCode == null), Arg.Any<CancellationToken>());
        }
        await _userRoleRepository.Received(1).AddAsync(Arg.Is<UserRole>(r => r != null && r.UserId == result.Value.User.Id), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserStatus.PendingApproval, ErrorCodes.AuthAccountPendingApproval)]
    [InlineData(UserStatus.Rejected, ErrorCodes.AuthAccountRejected)]
    [InlineData(UserStatus.Blocked, ErrorCodes.AuthUserBlocked)]
    [InlineData(UserStatus.Inactive, ErrorCodes.AuthUserInactive)]
    public async Task Should_Fail_Login_When_User_Is_Not_Active(UserStatus status, string expectedErrorCode)
    {
        var request = new GoogleLoginRequest { IdToken = "valid-google-id-token" };

        var googleUserInfo = new GoogleUserInfo
        {
            Email = "user@fpt.edu.vn",
            FullName = "Nguyen Van A",
            EmailVerified = true,
            Subject = "google-sub-id"
        };

        _googleAuthService.VerifyIdTokenAsync("valid-google-id-token", Arg.Any<CancellationToken>())
            .Returns(Result.Success(googleUserInfo));

        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "user@fpt.edu.vn",
            NormalizedEmail = "user@fpt.edu.vn",
            Status = status
        };

        _userRepository.GetByEmailWithRolesAsync("user@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error.Code);

        _jwtTokenService.DidNotReceiveWithAnyArgs().GenerateAccessToken(default!, default!);
    }
}

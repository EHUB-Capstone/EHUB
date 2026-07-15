using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.Login;
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

namespace EHub.ApplicationTests.Features.Auth.Login;

public class LoginCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();
    private readonly IRefreshTokenRepository _refreshTokenRepository = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwtTokenService = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenService _refreshTokenService = Substitute.For<IRefreshTokenService>();

    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _handler = new LoginCommandHandler(
            _userRepository,
            _studentRepository,
            _refreshTokenRepository,
            _unitOfWork,
            _passwordHasher,
            _jwtTokenService,
            _refreshTokenService);
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Login_Successfully_When_Credentials_Are_Valid_And_User_Active()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "student@fpt.edu.vn",
            Password = "Password123"
        };

        var userId = Guid.NewGuid();
        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            NormalizedEmail = "student@fpt.edu.vn",
            PasswordHash = "hashed_password",
            Status = UserStatus.Active
        };
        SetId(user, userId);

        var role = new Role { Name = SystemRoles.Student };
        SetId(role, Guid.NewGuid());
        user.UserRoles.Add(new UserRole { UserId = userId, Role = role });

        _userRepository.GetByEmailWithRolesAsync("student@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Password123", "hashed_password").Returns(true);
        
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
            MajorCode = MajorCodes.BIT_SE
        });

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("access_token", result.Value.AccessToken);
        Assert.Equal("raw_refresh_token", result.Value.RefreshToken);
        Assert.Equal(UserStatus.Active.ToString(), result.Value.User?.Status);
        Assert.Equal(MajorCodes.BIT_SE, result.Value.User?.MajorCode);

        await _refreshTokenRepository.Received(1).AddAsync(Arg.Any<EHub.Domain.Entities.RefreshToken>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_Login_When_User_Not_Found()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "nonexistent@fpt.edu.vn",
            Password = "Password123"
        };

        _userRepository.GetByEmailWithRolesAsync("nonexistent@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);

        _passwordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Fail_Login_When_Password_Is_Incorrect()
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "student@fpt.edu.vn",
            Password = "WrongPassword"
        };

        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "student@fpt.edu.vn",
            NormalizedEmail = "student@fpt.edu.vn",
            PasswordHash = "hashed_password",
            Status = UserStatus.Active
        };

        _userRepository.GetByEmailWithRolesAsync("student@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("WrongPassword", "hashed_password").Returns(false);

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, result.Error.Code);

        _jwtTokenService.DidNotReceiveWithAnyArgs().GenerateAccessToken(default!, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserStatus.PendingApproval, ErrorCodes.AuthAccountPendingApproval)]
    [InlineData(UserStatus.Rejected, ErrorCodes.AuthAccountRejected)]
    [InlineData(UserStatus.Blocked, ErrorCodes.AuthUserBlocked)]
    [InlineData(UserStatus.Inactive, ErrorCodes.AuthUserInactive)]
    public async Task Should_Fail_Login_When_User_Is_Not_Active(UserStatus status, string expectedErrorCode)
    {
        var request = new EmailPasswordLoginRequest
        {
            Email = "user@fpt.edu.vn",
            Password = "Password123"
        };

        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "user@fpt.edu.vn",
            NormalizedEmail = "user@fpt.edu.vn",
            PasswordHash = "hashed_password",
            Status = status
        };

        _userRepository.GetByEmailWithRolesAsync("user@fpt.edu.vn", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Password123", "hashed_password").Returns(true);

        var result = await _handler.HandleAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error.Code);

        _jwtTokenService.DidNotReceiveWithAnyArgs().GenerateAccessToken(default!, default!);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

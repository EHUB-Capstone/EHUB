using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Auth.GetCurrentUser;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using EHub.Shared.Errors;
using EHub.Domain.Common;

namespace EHub.UnitTests.Features.Auth.GetCurrentUser;

public class GetCurrentUserQueryHandlerTests
{
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IStudentRepository _studentRepository = Substitute.For<IStudentRepository>();

    private readonly GetCurrentUserQueryHandler _handler;

    public GetCurrentUserQueryHandlerTests()
    {
        _handler = new GetCurrentUserQueryHandler(
            _currentUserService,
            _userRepository,
            _studentRepository);
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Authenticated()
    {
        _currentUserService.IsAuthenticated.Returns(false);
        _currentUserService.UserId.Returns((Guid?)null);

        var result = await _handler.HandleAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.CommonUnauthorizedError, result.Error.Code);

        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdWithRolesAsync(default!, default!);
    }

    [Fact]
    public async Task Should_Fail_When_UserId_Null()
    {
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns((Guid?)null);

        var result = await _handler.HandleAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.CommonUnauthorizedError, result.Error.Code);

        await _userRepository.DidNotReceiveWithAnyArgs().GetByIdWithRolesAsync(default!, default!);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found_In_Db()
    {
        var userId = Guid.NewGuid();
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(userId);

        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        var result = await _handler.HandleAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.CommonUnauthorizedError, result.Error.Code);
    }

    [Fact]
    public async Task Should_Return_Active_Student_Data_Correctly()
    {
        var userId = Guid.NewGuid();
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(userId);

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

        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        
        _studentRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(new Student
        {
            UserId = userId,
            MajorCode = MajorCodes.BIT_SE
        });

        var result = await _handler.HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
        Assert.Equal("Nguyen Van A", result.Value.FullName);
        Assert.Equal("student@fpt.edu.vn", result.Value.Email);
        Assert.Contains(SystemRoles.Student, result.Value.Roles);
        Assert.Equal(UserStatus.Active.ToString(), result.Value.Status);
        Assert.Equal(MajorCodes.BIT_SE, result.Value.MajorCode);
    }

    [Fact]
    public async Task Should_Return_Active_Lecturer_Data_Correctly_With_Null_Major()
    {
        var userId = Guid.NewGuid();
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(userId);

        var user = new User
        {
            FullName = "Tran Van B",
            Email = "lecturer@fpt.edu.vn",
            NormalizedEmail = "lecturer@fpt.edu.vn",
            Status = UserStatus.Active
        };
        SetId(user, userId);

        var role = new Role { Name = SystemRoles.Lecturer };
        SetId(role, Guid.NewGuid());
        user.UserRoles.Add(new UserRole { UserId = userId, Role = role });

        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.HandleAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value.Id);
        Assert.Equal("Tran Van B", result.Value.FullName);
        Assert.Contains(SystemRoles.Lecturer, result.Value.Roles);
        Assert.Null(result.Value.MajorCode);

        await _studentRepository.DidNotReceiveWithAnyArgs().GetByUserIdAsync(default!, default!);
    }

    [Theory]
    [InlineData(UserStatus.PendingApproval, ErrorCodes.AuthAccountPendingApproval)]
    [InlineData(UserStatus.Rejected, ErrorCodes.AuthAccountRejected)]
    [InlineData(UserStatus.Blocked, ErrorCodes.AuthUserBlocked)]
    [InlineData(UserStatus.Inactive, ErrorCodes.AuthUserInactive)]
    public async Task Should_Fail_When_User_Is_Not_Active(UserStatus status, string expectedErrorCode)
    {
        var userId = Guid.NewGuid();
        _currentUserService.IsAuthenticated.Returns(true);
        _currentUserService.UserId.Returns(userId);

        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "user@fpt.edu.vn",
            NormalizedEmail = "user@fpt.edu.vn",
            Status = status
        };
        SetId(user, userId);

        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _handler.HandleAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error.Code);

        await _studentRepository.DidNotReceiveWithAnyArgs().GetByUserIdAsync(default!, default!);
    }
}

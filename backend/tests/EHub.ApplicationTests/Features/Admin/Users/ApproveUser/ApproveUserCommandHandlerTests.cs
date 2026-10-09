using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Admin.Users.ApproveUser;
using EHub.Application.Features.Admin.Users;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Domain.Common;

using Microsoft.Extensions.Logging;

namespace EHub.ApplicationTests.Features.Admin.Users.ApproveUser;

public class ApproveUserCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly ILogger<ApproveUserCommandHandler> _logger = Substitute.For<ILogger<ApproveUserCommandHandler>>();
    private readonly ApproveUserCommandHandler _handler;

    public ApproveUserCommandHandlerTests()
    {
        _handler = new ApproveUserCommandHandler(
            _userRepository,
            _unitOfWork,
            _currentUserService,
            _logger);
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        // Act
        var result = await _handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.UserNotFound, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Pending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User { Status = UserStatus.Active };
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ApprovalUserNotPending, result.Error.Code);
    }

    [Fact]
    public async Task Should_Fail_When_Role_Is_Not_Approval_Target()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new User
        {
            Status = UserStatus.PendingApproval,
            UserRoles = new List<UserRole> { new UserRole { Role = new Role { Name = SystemRoles.Student } } }
        };
        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);

        // Act
        var result = await _handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ApprovalInvalidTargetRole, result.Error.Code);
    }

    [Fact]
    public async Task Should_Approve_Successfully_When_Lecturer_Pending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = new User
        {
            Status = UserStatus.PendingApproval,
            UserRoles = new List<UserRole> { new UserRole { Role = new Role { Name = SystemRoles.Lecturer } } }
        };
        SetId(user, userId);

        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _currentUserService.UserId.Returns(adminId);

        // Act
        var result = await _handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(adminId, user.UpdatedBy);

        _userRepository.Received(1).Update(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Approve_Successfully_When_Mentor_Pending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var user = new User
        {
            Status = UserStatus.PendingApproval,
            UserRoles = new List<UserRole> { new UserRole { Role = new Role { Name = SystemRoles.Mentor } } }
        };
        SetId(user, userId);

        _userRepository.GetByIdWithRolesAsync(userId, Arg.Any<CancellationToken>()).Returns(user);
        _currentUserService.UserId.Returns(adminId);

        // Act
        var result = await _handler.HandleAsync(userId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(adminId, user.UpdatedBy);

        _userRepository.Received(1).Update(user);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

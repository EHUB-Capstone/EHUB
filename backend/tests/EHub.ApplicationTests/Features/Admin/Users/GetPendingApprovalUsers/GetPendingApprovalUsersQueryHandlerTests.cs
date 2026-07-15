using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using EHub.Application.Features.Admin.Users.GetPendingApprovalUsers;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Contracts.Admin.Users;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Domain.Common;
using EHub.Shared.Constants;

namespace EHub.ApplicationTests.Features.Admin.Users.GetPendingApprovalUsers;

public class GetPendingApprovalUsersQueryHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly GetPendingApprovalUsersQueryHandler _handler;

    public GetPendingApprovalUsersQueryHandlerTests()
    {
        _handler = new GetPendingApprovalUsersQueryHandler(_userRepository);
    }

    private static void SetId(BaseEntity entity, Guid id)
    {
        var property = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        property?.SetValue(entity, id);
    }

    [Fact]
    public async Task Should_Return_Pending_Users_Successfully()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var user1 = new User
        {
            FullName = "Lecturer A",
            Email = "lecturer@test.com",
            Status = UserStatus.PendingApproval,
            UserRoles = new List<UserRole> { new UserRole { Role = new Role { Name = SystemRoles.Lecturer } } }
        };
        SetId(user1, userId1);

        var userId2 = Guid.NewGuid();
        var user2 = new User
        {
            FullName = "Mentor B",
            Email = "mentor@test.com",
            Status = UserStatus.PendingApproval,
            UserRoles = new List<UserRole> { new UserRole { Role = new Role { Name = SystemRoles.Mentor } } }
        };
        SetId(user2, userId2);

        var users = new[] { user1, user2 };

        _userRepository.GetPendingApprovalUsersAsync(Arg.Any<CancellationToken>()).Returns(users);

        // Act
        var result = await _handler.HandleAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(2, result.Value.Count);
        
        var response1 = result.Value.First(u => u.Id == userId1);
        Assert.Equal("Lecturer A", response1.FullName);
        Assert.Equal("lecturer@test.com", response1.Email);
        Assert.Contains(SystemRoles.Lecturer, response1.Roles);
        Assert.Equal(UserStatus.PendingApproval.ToString(), response1.Status);

        var response2 = result.Value.First(u => u.Id == userId2);
        Assert.Equal("Mentor B", response2.FullName);
        Assert.Equal("mentor@test.com", response2.Email);
        Assert.Contains(SystemRoles.Mentor, response2.Roles);
        Assert.Equal(UserStatus.PendingApproval.ToString(), response2.Status);
    }
}

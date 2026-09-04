using EHub.Application.Features.Classes.Common;
using EHub.Shared.Constants;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Classes.Common;

public sealed class ClassAuthorizationRulesTests
{
    [Fact]
    public void CanManageClass_WhenUserIsAdmin_ReturnsTrueForAnyClass()
    {
        ClassAuthorizationRules.CanManageClass(Guid.NewGuid(), Guid.NewGuid(), SystemRoles.Admin)
            .Should().BeTrue();
    }

    [Fact]
    public void CanManageClass_WhenLecturerIsAssigned_ReturnsTrue()
    {
        var lecturerId = Guid.NewGuid();

        ClassAuthorizationRules.CanManageClass(lecturerId, lecturerId, SystemRoles.Lecturer)
            .Should().BeTrue();
    }

    [Fact]
    public void CanManageClass_WhenLecturerIsNotAssigned_ReturnsFalse()
    {
        ClassAuthorizationRules.CanManageClass(Guid.NewGuid(), Guid.NewGuid(), SystemRoles.Lecturer)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    [InlineData("")]
    public void CanManageClass_WhenRoleIsNotStaff_ReturnsFalse(string role)
    {
        var userId = Guid.NewGuid();

        ClassAuthorizationRules.CanManageClass(userId, userId, role)
            .Should().BeFalse();
    }
}

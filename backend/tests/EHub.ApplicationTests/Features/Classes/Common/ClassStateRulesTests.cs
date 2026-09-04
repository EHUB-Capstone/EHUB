using EHub.Application.Features.Classes.Common;
using EHub.Domain.Enums;
using EHub.Shared.Errors;
using FluentAssertions;

namespace EHub.ApplicationTests.Features.Classes.Common;

public sealed class ClassStateRulesTests
{
    [Theory]
    [InlineData(ClassStatus.Completed)]
    [InlineData(ClassStatus.Archived)]
    public void IsReadOnly_ForHistoricalStatuses_ReturnsTrue(ClassStatus status)
    {
        ClassStateRules.IsReadOnly(status).Should().BeTrue();
        ClassStateRules.GetMutationError(status).Should().NotBeNull();
    }

    [Theory]
    [InlineData(ClassStatus.Draft)]
    [InlineData(ClassStatus.Active)]
    public void IsOperational_ForMutableStatuses_ReturnsTrue(ClassStatus status)
    {
        ClassStateRules.IsOperational(status).Should().BeTrue();
        ClassStateRules.GetMutationError(status).Should().BeNull();
    }

    [Fact]
    public void GetMutationError_ForCompletedClass_UsesDedicatedErrorCode()
    {
        ClassStateRules.GetMutationError(ClassStatus.Completed)!.Code
            .Should().Be(ErrorCodes.ClassCompleted);
    }
}

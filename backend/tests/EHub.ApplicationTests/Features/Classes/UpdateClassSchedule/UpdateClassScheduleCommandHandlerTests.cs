using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.UpdateClassSchedule;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.UpdateClassSchedule;

public class UpdateClassScheduleCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly UpdateClassScheduleCommandHandler _handler;

    public UpdateClassScheduleCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _handler = new UpdateClassScheduleCommandHandler(_context);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Arrange
        var request = new UpdateClassScheduleRequest
        {
            Schedules = new List<ClassScheduleSlotDto>
            {
                new() { DayOfWeek = DayOfWeek.Monday, SlotNumber = 1 }
            }
        };

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.AccessDenied");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(-1)]
    public async Task HandleAsync_WhenSlotNumberIsInvalid_ReturnsInvalidSlotNumberError(int invalidSlot)
    {
        // Arrange
        var request = new UpdateClassScheduleRequest
        {
            Schedules = new List<ClassScheduleSlotDto>
            {
                new() { DayOfWeek = DayOfWeek.Monday, SlotNumber = invalidSlot }
            }
        };

        // Act
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Admin);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Classes.InvalidSlotNumber");
    }
}

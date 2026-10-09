using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.ExportAdminClassData;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ExportAdminClassData;

public sealed class ExportAdminClassDataQueryHandlerValidationTests
{
    private readonly ExportAdminClassDataQueryHandler _handler =
        new(Substitute.For<IApplicationDbContext>());

    [Fact]
    public async Task HandleAsync_WhenRoleIsNotAdmin_ReturnsAccessDenied()
    {
        var result = await _handler.HandleAsync(ValidRequest(), SystemRoles.Lecturer);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenSelectionIsEmpty_ReturnsValidationError()
    {
        var request = ValidRequest(classIds: Array.Empty<Guid>());

        var result = await _handler.HandleAsync(request, SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public async Task HandleAsync_WhenSelectionContainsDuplicates_ReturnsValidationError()
    {
        var classId = Guid.NewGuid();
        var request = ValidRequest(classIds: [classId, classId]);

        var result = await _handler.HandleAsync(request, SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Theory]
    [InlineData("", 2026)]
    [InlineData("INVALID", 2026)]
    [InlineData("FA", 1999)]
    [InlineData("FA", 2101)]
    public async Task HandleAsync_WhenSemesterOrYearIsInvalid_ReturnsValidationError(string semester, int year)
    {
        var request = ValidRequest(semester, year);

        var result = await _handler.HandleAsync(request, SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    private static ExportAdminClassDataRequest ValidRequest(
        string semester = "FA",
        int year = 2026,
        IReadOnlyCollection<Guid>? classIds = null) => new()
    {
        Semester = semester,
        Year = year,
        ClassIds = classIds ?? [Guid.NewGuid()]
    };
}

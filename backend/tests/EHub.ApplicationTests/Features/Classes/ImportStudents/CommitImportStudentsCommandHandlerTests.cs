using System;
using System.Threading.Tasks;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Features.Classes.ImportStudents;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace EHub.ApplicationTests.Features.Classes.ImportStudents;

public class CommitImportStudentsCommandHandlerTests
{
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommitImportStudentsCommandHandler _handler;

    public CommitImportStudentsCommandHandlerTests()
    {
        _context = Substitute.For<IApplicationDbContext>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _handler = new CommitImportStudentsCommandHandler(_context, _unitOfWork);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsStudent_ReturnsAccessDeniedError()
    {
        // Act
        var request = new CommitImportStudentsRequest { SessionId = Guid.NewGuid() };
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Student);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
    }

    [Fact]
    public async Task HandleAsync_WhenLecturerUsesEmptySessionId_ReturnsValidationError()
    {
        var request = new CommitImportStudentsRequest { SessionId = Guid.Empty };
        var result = await _handler.HandleAsync(Guid.NewGuid(), request, Guid.NewGuid(), SystemRoles.Lecturer);

        result.Error.Code.Should().Be(ErrorCodes.ClassValidationError);
    }

    [Fact]
    public void ResolveImportedLeaderStudentCode_WhenMetadataBelongsToOneRollNumber_ReturnsThatStudent()
    {
        var rows = new[]
        {
            Row("SE001", zaloGroupUrl: "https://zalo.me/g/example"),
            Row("SE002"),
            Row("SE003"),
            Row("SE004")
        };

        var result = CommitImportStudentsCommandHandler.ResolveImportedLeaderStudentCode(rows);

        result.Should().Be("SE001");
    }

    [Fact]
    public void ResolveImportedLeaderStudentCode_WhenOnlyDescriptionIsProvided_ReturnsItsStudent()
    {
        var rows = new[]
        {
            Row("SE001"),
            Row("SE002", description: "A sufficiently detailed project description."),
            Row("SE003"),
            Row("SE004")
        };

        var result = CommitImportStudentsCommandHandler.ResolveImportedLeaderStudentCode(rows);

        result.Should().Be("SE002");
    }

    [Fact]
    public void ResolveImportedLeaderStudentCode_WhenZaloAndDescriptionBelongToDifferentStudents_ReturnsNull()
    {
        var rows = new[]
        {
            Row("SE001", zaloGroupUrl: "https://zalo.me/g/example"),
            Row("SE002", description: "A sufficiently detailed project description."),
            Row("SE003"),
            Row("SE004")
        };

        var result = CommitImportStudentsCommandHandler.ResolveImportedLeaderStudentCode(rows);

        result.Should().BeNull();
    }

    [Fact]
    public void ResolveImportedLeaderStudentCode_WhenTeamHasNoZaloOrDescription_ReturnsNull()
    {
        var rows = new[] { Row("SE001"), Row("SE002"), Row("SE003"), Row("SE004") };

        var result = CommitImportStudentsCommandHandler.ResolveImportedLeaderStudentCode(rows);

        result.Should().BeNull();
    }

    private static ImportStudentRowPreviewDto Row(
        string studentCode,
        string? zaloGroupUrl = null,
        string? description = null) => new()
    {
        StudentCode = studentCode,
        ZaloGroupUrl = zaloGroupUrl,
        ProjectDescription = description
    };
}

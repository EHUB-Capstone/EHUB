using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Classes.RepairChatMemberships;
using EHub.Contracts.Classes;
using EHub.Shared.Constants;
using EHub.Shared.Errors;
using EHub.Shared.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EHub.ApplicationTests.Features.Classes.RepairChatMemberships;

public sealed class RepairClassChatMembershipsCommandHandlerTests
{
    private readonly IApplicationDbContext _context = Substitute.For<IApplicationDbContext>();
    private readonly IClassChatMembershipSynchronizer _synchronizer = Substitute.For<IClassChatMembershipSynchronizer>();
    private readonly ILogger<RepairClassChatMembershipsCommandHandler> _logger = Substitute.For<ILogger<RepairClassChatMembershipsCommandHandler>>();

    [Theory]
    [InlineData(SystemRoles.Student)]
    [InlineData(SystemRoles.Mentor)]
    public async Task HandleAsync_WhenUserIsNotStaff_ReturnsAccessDenied(string role)
    {
        var handler = new RepairClassChatMembershipsCommandHandler(
            _context,
            Substitute.For<IUnitOfWork>(),
            _synchronizer,
            _logger);

        var result = await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), role);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassAccessDenied);
        await _synchronizer.DidNotReceiveWithAnyArgs().SynchronizeAsync(default);
    }

    [Fact]
    public async Task HandleAsync_WhenRepairThrows_ReturnsSanitizedServerError()
    {
        const string sensitiveMessage = "password=secret; relation internal_table does not exist";
        var handler = new RepairClassChatMembershipsCommandHandler(
            _context,
            new ThrowingUnitOfWork(new InvalidOperationException(sensitiveMessage)),
            _synchronizer,
            _logger);

        var result = await handler.HandleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SystemRoles.Admin);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.ClassChatMembershipRepairFailed);
        result.Error.Message.Should().NotContain("secret").And.NotContain("internal_table");
    }

    private sealed class ThrowingUnitOfWork(Exception exception) : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TResult> ExecuteInSerializableTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> action,
            CancellationToken cancellationToken = default) =>
            Task.FromException<TResult>(exception);
    }
}

using System.Text.Json;
using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Services;
using EHub.Contracts.Classes;
using EHub.Contracts.Teams;
using EHub.Domain.Entities;
using EHub.Domain.Enums;
using EHub.Infrastructure.BackgroundJobs;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EHub.IntegrationTests.Notifications;

[Collection("Sequential")]
public sealed class NotificationEmailOutboxIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task RosterFanout_RetryOnlyFailedRecipient_AndReplayDoesNotDuplicateDeliveries()
    {
        var seed = await SeedRosterAsync();
        var sender = new RecordingEmailService { FailOnceFor = seed.Second.Email };
        await using var services = CreateServices(sender);
        var processor = CreateProcessor(services);

        await processor.ProcessAsync(seed.Message.Id, CancellationToken.None);

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parent = await context.OutboxMessages.SingleAsync(item => item.Id == seed.Message.Id);
        parent.Status.Should().Be(OutboxMessageStatus.Processed);
        (await context.Notifications.CountAsync(item => item.SourceEventId == parent.EventId)).Should().Be(2);
        sender.Attempts.Should().BeEmpty("projecting the roster only queues email work");

        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxEventDispatcher>();
        await dispatcher.DispatchAsync(parent);
        var children = await context.OutboxMessages
            .Where(item => item.AggregateId == parent.AggregateId && item.Type == NotificationOutboxEventDispatcher.ClassEmailEventType)
            .ToArrayAsync();
        children.Should().HaveCount(2, "duplicate recipient entries and source replays share one deterministic event ID");
        var first = children.Single(item => Recipient(item) == seed.First.Email);
        var second = children.Single(item => Recipient(item) == seed.Second.Email);

        await MarkProcessingAsync(first.Id);
        await processor.ProcessAsync(first.Id, CancellationToken.None);
        await MarkProcessingAsync(second.Id);
        await processor.ProcessAsync(second.Id, CancellationToken.None);

        context.ChangeTracker.Clear();
        (await context.OutboxMessages.SingleAsync(item => item.Id == first.Id)).Status.Should().Be(OutboxMessageStatus.Processed);
        var failedRecipient = await context.OutboxMessages.SingleAsync(item => item.Id == second.Id);
        failedRecipient.Status.Should().Be(OutboxMessageStatus.Pending);
        failedRecipient.LastError.Should().Be(nameof(EmailDeliveryFailureKind.Connection));

        await MarkProcessingAsync(second.Id);
        await processor.ProcessAsync(second.Id, CancellationToken.None);
        await processor.ProcessAsync(first.Id, CancellationToken.None);
        sender.Attempts.Count(email => email == seed.First.Email).Should().Be(1);
        sender.Attempts.Count(email => email == seed.Second.Email).Should().Be(2);
        sender.Delivered.Should().BeEquivalentTo([seed.First.Email, seed.Second.Email]);
        context.ChangeTracker.Clear();
        (await context.OutboxMessages.SingleAsync(item => item.Id == second.Id)).Status.Should().Be(OutboxMessageStatus.Processed);
    }

    [Fact]
    public async Task FailedProjection_RollsBackNotificationsAndFanout_BeforeSavingRetry()
    {
        var seed = await SeedRosterAsync();
        var sender = new RecordingEmailService();
        await using (var failingServices = CreateServices(sender, failAfterProjection: true))
        {
            await CreateProcessor(failingServices).ProcessAsync(seed.Message.Id, CancellationToken.None);
        }

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parent = await context.OutboxMessages.SingleAsync(item => item.Id == seed.Message.Id);
        parent.Status.Should().Be(OutboxMessageStatus.Pending);
        parent.LastError.Should().Be(nameof(InvalidOperationException));
        (await context.Notifications.AnyAsync(item => item.SourceEventId == parent.EventId)).Should().BeFalse();
        (await context.OutboxMessages.AnyAsync(item => item.AggregateId == parent.AggregateId
            && item.Type == NotificationOutboxEventDispatcher.ClassEmailEventType)).Should().BeFalse();
        sender.Attempts.Should().BeEmpty();

        await using var services = CreateServices(sender);
        await MarkProcessingAsync(parent.Id);
        await CreateProcessor(services).ProcessAsync(parent.Id, CancellationToken.None);
        context.ChangeTracker.Clear();
        (await context.OutboxMessages.SingleAsync(item => item.Id == parent.Id)).Status.Should().Be(OutboxMessageStatus.Processed);
        (await context.Notifications.CountAsync(item => item.SourceEventId == parent.EventId)).Should().Be(2);
        (await context.OutboxMessages.CountAsync(item => item.AggregateId == parent.AggregateId
            && item.Type == NotificationOutboxEventDispatcher.ClassEmailEventType)).Should().Be(2);
    }

    [Fact]
    public async Task ClassCreated_QueuesLecturerEmail_AndProcessedDeliveryIsNotSentAgain()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lecturer = CreateUser("lecturer");
        context.Users.Add(lecturer);
        var parent = CreateMessage("Class.Created.v1", new { primaryLecturerId = lecturer.Id });
        context.OutboxMessages.Add(parent);
        await context.SaveChangesAsync();

        var sender = new RecordingEmailService();
        await using var services = CreateServices(sender);
        var processor = CreateProcessor(services);
        await processor.ProcessAsync(parent.Id, CancellationToken.None);
        var child = await context.OutboxMessages.SingleAsync(item => item.AggregateId == parent.AggregateId
            && item.Type == NotificationOutboxEventDispatcher.ClassEmailEventType);
        Recipient(child).Should().Be(lecturer.Email);
        using var payload = JsonDocument.Parse(child.PayloadJson);
        payload.RootElement.GetProperty("data").GetProperty("subject").GetString().Should().Contain("Teaching Assignment");
        await MarkProcessingAsync(child.Id);
        await processor.ProcessAsync(child.Id, CancellationToken.None);
        await processor.ProcessAsync(child.Id, CancellationToken.None);
        sender.Delivered.Should().ContainSingle().Which.Should().Be(lecturer.Email);
    }

    [Fact]
    public async Task QuotaExceeded_UsesCooldown_AndDoesNotExhaustRecipientRetryBudget()
    {
        var retryAt = DateTime.UtcNow.AddMinutes(20);
        var sender = new RecordingEmailService
        {
            Failure = new EmailDeliveryException(EmailDeliveryFailureKind.QuotaExceeded, retryAt)
        };
        await using var services = CreateServices(sender);
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = CreateMessage(NotificationOutboxEventDispatcher.ClassEmailEventType,
            new { email = "recipient@example.test", fullName = "Recipient", subject = "Class", title = "Class", body = "Assigned" });
        message.AttemptCount = 10;
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();

        await CreateProcessor(services).ProcessAsync(message.Id, CancellationToken.None);

        await context.Entry(message).ReloadAsync();
        message.Status.Should().Be(OutboxMessageStatus.Pending);
        message.AttemptCount.Should().Be(9);
        message.AvailableAtUtc.Should().BeCloseTo(retryAt, TimeSpan.FromMilliseconds(1));
        message.LastError.Should().Be(nameof(EmailDeliveryFailureKind.QuotaExceeded));
    }

    private ServiceProvider CreateServices(RecordingEmailService sender, bool failAfterProjection = false)
    {
        using var scope = factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new AppDbContext(options));
        services.AddSingleton<IEmailService>(sender);
        services.AddSingleton<IClassChatMembershipSynchronizer, NoOpChatSynchronizer>();
        var realtimePublisher = new NoOpRealtimePublisher();
        services.AddSingleton<IProjectDirectionRealtimePublisher>(realtimePublisher);
        services.AddSingleton<IClassRealtimePublisher>(realtimePublisher);
        services.AddScoped<NotificationOutboxEventDispatcher>();
        services.AddScoped<IOutboxEventDispatcher>(provider => failAfterProjection
            ? new FailAfterProjection(provider.GetRequiredService<NotificationOutboxEventDispatcher>())
            : provider.GetRequiredService<NotificationOutboxEventDispatcher>());
        return services.BuildServiceProvider();
    }

    private static OutboxProcessorBackgroundService CreateProcessor(ServiceProvider services) =>
        new(services.GetRequiredService<IServiceScopeFactory>(), NullLogger<OutboxProcessorBackgroundService>.Instance);

    private async Task MarkProcessingAsync(Guid messageId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var message = await context.OutboxMessages.SingleAsync(item => item.Id == messageId);
        message.Status = OutboxMessageStatus.Processing;
        message.ProcessingStartedAtUtc = DateTime.UtcNow;
        message.AttemptCount++;
        await context.SaveChangesAsync();
    }

    private async Task<(OutboxMessage Message, User First, User Second)> SeedRosterAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = CreateUser("first");
        var second = CreateUser("second");
        context.Users.AddRange(first, second);
        var message = CreateMessage("Class.StudentRosterImported.v1", new
        {
            studentUserIds = new[] { first.Id, second.Id, first.Id },
            studentRecipients = new[]
            {
                new { email = first.Email, fullName = first.FullName },
                new { email = second.Email, fullName = second.FullName },
                new { email = $" {first.Email.ToUpperInvariant()} ", fullName = first.FullName }
            }
        });
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync();
        return (message, first, second);
    }

    private static User CreateUser(string name)
    {
        var email = $"{name}-{Guid.NewGuid():N}@example.test";
        return new User { Email = email, NormalizedEmail = email.ToUpperInvariant(), FullName = name, Status = UserStatus.Active };
    }

    private static OutboxMessage CreateMessage(string type, object data) => new()
    {
        Type = type,
        AggregateId = Guid.NewGuid(),
        AggregateType = "Class",
        PayloadJson = JsonSerializer.Serialize(new { data }),
        Status = OutboxMessageStatus.Processing,
        ProcessingStartedAtUtc = DateTime.UtcNow,
        AttemptCount = 1
    };

    private static string Recipient(OutboxMessage message)
    {
        using var payload = JsonDocument.Parse(message.PayloadJson);
        return payload.RootElement.GetProperty("data").GetProperty("email").GetString()!;
    }

    private sealed class FailAfterProjection(NotificationOutboxEventDispatcher inner) : IOutboxEventDispatcher
    {
        public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            await inner.DispatchAsync(message, cancellationToken);
            throw new InvalidOperationException("Provider diagnostics must not be persisted.");
        }

        public Task PublishAfterCommitAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            inner.PublishAfterCommitAsync(message, cancellationToken);
    }

    private sealed class RecordingEmailService : IEmailService
    {
        public List<string> Attempts { get; } = [];
        public List<string> Delivered { get; } = [];
        public string? FailOnceFor { get; init; }
        public EmailDeliveryException? Failure { get; init; }

        public Task SendClassNotificationAsync(string toEmail, string fullName, string subject, string title, string message,
            CancellationToken cancellationToken = default)
        {
            Attempts.Add(toEmail);
            if (Failure != null) throw Failure;
            if (toEmail == FailOnceFor && Attempts.Count(email => email == toEmail) == 1)
                throw new EmailDeliveryException(EmailDeliveryFailureKind.Connection);
            Delivered.Add(toEmail);
            return Task.CompletedTask;
        }

        public Task SendRegistrationOtpAsync(string toEmail, string fullName, string otp, DateTime expiresAtUtc, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetUrl, DateTime expiresAt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SendPasswordChangedNotificationAsync(string toEmail, string fullName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class NoOpChatSynchronizer : IClassChatMembershipSynchronizer
    {
        public Task<ChatMembershipSyncResponse> SynchronizeAsync(Guid classId, Guid? requestedByUserId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatMembershipSyncResponse { ClassId = classId });
    }

    private sealed class NoOpRealtimePublisher : IProjectDirectionRealtimePublisher, IClassRealtimePublisher
    {
        public Task PublishAsync(IReadOnlyCollection<Guid> recipientUserIds, string eventType, Guid classId, Guid teamId, ProjectDirectionDto direction, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishNotificationReadyAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid classId, Guid teamId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishMajorUpdatedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid classId, Guid studentId, string majorCode, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishProposalReviewedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid classId, Guid proposalId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishTeamFormationChangedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid classId, Guid formationId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishCheckpointRequirementsUpdatedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid teamId, int checkpointNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PublishCheckpointEvaluationUpdatedAsync(IReadOnlyCollection<Guid> recipientUserIds, Guid teamId, int checkpointNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

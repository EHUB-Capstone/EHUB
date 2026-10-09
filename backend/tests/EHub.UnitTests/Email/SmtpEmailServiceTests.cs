using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Services;
using EHub.Infrastructure.Options;
using EHub.Infrastructure.Services.Email;
using FluentAssertions;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EHub.UnitTests.Email;

public sealed class SmtpEmailServiceTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private const string ProviderPrivateMessage = "provider-private-detail@example.test";

    [Fact]
    public async Task Send_WhenGmailQuotaExceeded_BlocksOtherRequestsUntilCooldownExpires()
    {
        var client = CreateClient();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)550,
                $"5.4.5 Daily user sending limit exceeded {ProviderPrivateMessage}"));
        var factory = CreateFactory(client);
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        var cooldown = new SmtpProviderCooldown();
        var first = CreateService(factory, cooldown: cooldown, clock: clock);
        var next = CreateService(factory, cooldown: cooldown, clock: clock);

        var initial = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(first));
        var blocked = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(next));

        initial.FailureKind.Should().Be(EmailDeliveryFailureKind.QuotaExceeded);
        initial.RetryAfterUtc.Should().Be(Now.AddMinutes(15));
        blocked.RetryAfterUtc.Should().Be(initial.RetryAfterUtc);
        initial.InnerException.Should().BeNull();
        initial.ToString().Should().NotContain(ProviderPrivateMessage);
        factory.Received(1).Create();
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());

        clock.UtcNow.Returns(Now.AddMinutes(15));
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(Task.FromResult("accepted"));

        await SendAsync(next);

        factory.Received(2).Create();
    }

    [Theory]
    [InlineData("smtp.example.test", "5.4.5 Daily user sending limit exceeded")]
    [InlineData("smtp.gmail.com", "5.4.5 Other provider rejection")]
    public async Task Send_WhenNotAnExplicitGmailDailyLimit_DoesNotOpenQuotaCircuit(string host, string response)
    {
        var client = CreateClient();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)550, response));
        var factory = CreateFactory(client);
        var service = CreateService(factory, options: CreateOptions(host));

        var first = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));
        var second = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));

        first.FailureKind.Should().Be(EmailDeliveryFailureKind.ProviderRejected);
        second.RetryAfterUtc.Should().BeNull();
        factory.Received(2).Create();
    }

    [Theory]
    [InlineData(SmtpErrorCode.SenderNotAccepted, EmailDeliveryFailureKind.SenderRejected)]
    [InlineData(SmtpErrorCode.RecipientNotAccepted, EmailDeliveryFailureKind.RecipientRejected)]
    [InlineData(SmtpErrorCode.MessageNotAccepted, EmailDeliveryFailureKind.ProviderRejected)]
    public async Task Send_WhenSmtpRejects_ClassifiesWithoutExposingProviderMessage(
        SmtpErrorCode code, EmailDeliveryFailureKind expected)
    {
        var client = CreateClient();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .ThrowsAsync(new SmtpCommandException(code, (SmtpStatusCode)550, ProviderPrivateMessage));
        var service = CreateService(CreateFactory(client));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));

        failure.FailureKind.Should().Be(expected);
        failure.InnerException.Should().BeNull();
        failure.ToString().Should().NotContain(ProviderPrivateMessage);
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Theory]
    [InlineData("authentication", EmailDeliveryFailureKind.Authentication)]
    [InlineData("tls", EmailDeliveryFailureKind.Tls)]
    [InlineData("connection", EmailDeliveryFailureKind.Connection)]
    [InlineData("timeout", EmailDeliveryFailureKind.Timeout)]
    [InlineData("unknown", EmailDeliveryFailureKind.Unknown)]
    public async Task Send_WhenTransportFails_ClassifiesWithoutKeepingSensitiveInnerException(
        string error, EmailDeliveryFailureKind expected)
    {
        Exception providerException = error switch
        {
            "authentication" => new MailKit.Security.AuthenticationException(ProviderPrivateMessage),
            "tls" => new System.Security.Authentication.AuthenticationException(ProviderPrivateMessage),
            "connection" => new IOException(ProviderPrivateMessage),
            "timeout" => new TimeoutException(ProviderPrivateMessage),
            _ => new InvalidOperationException(ProviderPrivateMessage)
        };
        var client = CreateClient();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(providerException);
        var service = CreateService(CreateFactory(client));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));

        failure.FailureKind.Should().Be(expected);
        failure.ToString().Should().NotContain(ProviderPrivateMessage);
        failure.InnerException.Should().BeNull();
        await client.DidNotReceive().SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task Send_WhenAuthenticationFails_NeverSends()
    {
        var client = CreateClient();
        client.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.UnexpectedStatusCode, (SmtpStatusCode)535, ProviderPrivateMessage));
        var service = CreateService(CreateFactory(client));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));

        failure.FailureKind.Should().Be(EmailDeliveryFailureKind.Authentication);
        await client.DidNotReceive().SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task Send_WhenOperationTimeoutExpires_CancelsTransportAndReportsTimeout()
    {
        var client = CreateClient();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));
        var service = CreateService(CreateFactory(client), options: CreateOptions(timeoutSeconds: 1));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));

        failure.FailureKind.Should().Be(EmailDeliveryFailureKind.Timeout);
        client.Received().Timeout = 1000;
        client.Received(1).Dispose();
    }

    [Fact]
    public async Task Send_WhenCallerCancels_PropagatesCancellation()
    {
        using var caller = new CancellationTokenSource();
        var client = CreateClient();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                caller.Cancel();
                return Task.FromCanceled(call.Arg<CancellationToken>());
            });
        var service = CreateService(CreateFactory(client));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SendAsync(service, caller.Token));

        client.Received(1).Dispose();
        await client.DidNotReceive().SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task Send_WhenAlreadyCancelled_DoesNotCreateClient()
    {
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        var factory = CreateFactory(CreateClient());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SendAsync(CreateService(factory), caller.Token));

        factory.DidNotReceive().Create();
    }

    [Fact]
    public async Task Send_WhenDisconnectAndDisposeFailAfterAcceptance_SucceedsWithoutSensitiveLogs()
    {
        var client = CreateClient();
        client.DisconnectAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new IOException(ProviderPrivateMessage));
        client.When(value => value.Dispose()).Do(_ => throw new IOException(ProviderPrivateMessage));
        var logger = new RecordingLogger();
        var service = CreateService(CreateFactory(client), logger: logger);

        await SendAsync(service);

        logger.Messages.Should().Contain(message => message.Contains("email remains accepted"));
        logger.Messages.Should().OnlyContain(message => !message.Contains(ProviderPrivateMessage));
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task Send_WhenCallerCancelsAfterSmtpAcceptance_DoesNotReportDeliveryFailure()
    {
        using var caller = new CancellationTokenSource();
        var client = CreateClient();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(_ =>
            {
                caller.Cancel();
                return Task.FromResult("accepted");
            });

        await SendAsync(CreateService(CreateFactory(client)), caller.Token);

        await client.Received(1).DisconnectAsync(false, Arg.Is<CancellationToken>(token => !token.IsCancellationRequested));
    }

    [Fact]
    public async Task Send_WhenDeliveryAndDisposeBothFail_PreservesOriginalFailureKind()
    {
        var client = CreateClient();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .ThrowsAsync(new IOException(ProviderPrivateMessage));
        client.When(value => value.Dispose()).Do(_ => throw new InvalidOperationException(ProviderPrivateMessage));

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(CreateService(CreateFactory(client))));

        failure.FailureKind.Should().Be(EmailDeliveryFailureKind.Connection);
    }

    [Fact]
    public async Task Send_WhenConfigurationMissing_FailsBeforeConnecting()
    {
        var factory = CreateFactory(CreateClient());
        var service = CreateService(factory, options: new EmailOptions());

        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));

        failure.FailureKind.Should().Be(EmailDeliveryFailureKind.Configuration);
        factory.DidNotReceive().Create();
    }

    private static Task SendAsync(SmtpEmailService service, CancellationToken cancellationToken = default) =>
        service.SendClassNotificationAsync("recipient@example.test", "Test recipient", "Test subject", "Title", "Body", cancellationToken);

    [Fact]
    public async Task Send_AfterAcceptance_DoesNotWaitForNetworkQuit()
    {
        var client = CreateClient();
        client.DisconnectAsync(true, Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));

        await SendAsync(CreateService(CreateFactory(client)));

        await client.Received(1).DisconnectAsync(false, Arg.Any<CancellationToken>());
        await client.DidNotReceive().DisconnectAsync(true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Fallback_QuotaThenCooldown_UsesBrevoWithSameOtpAndReturnsToGmail()
    {
        var gmail = CreateClient();
        var brevo = CreateClient();
        string? gmailBody = null;
        string? brevoBody = null;
        gmail.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(call =>
            {
                gmailBody = call.Arg<MimeMessage>()!.TextBody;
                return Task.FromException<string>(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)550,
                    "5.4.5 Daily user sending limit exceeded"));
            });
        brevo.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(call =>
            {
                brevoBody = call.Arg<MimeMessage>()!.TextBody;
                call.Arg<MimeMessage>()!.From.Mailboxes.Single().Address.Should().Be("brevo@example.test");
                return Task.FromResult("accepted");
            });
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(gmail, brevo, brevo, gmail);
        var clock = Substitute.For<IDateTimeProvider>();
        var service = CreateService(factory, FallbackOptions(), clock: clock);
        await service.SendRegistrationOtpAsync("recipient@example.test", "Test", "123456", Now.AddMinutes(5));
        brevoBody.Should().Be(gmailBody);
        await SendAsync(service);
        await gmail.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
        await brevo.Received(2).ConnectAsync("smtp-relay.brevo.com", 587, SecureSocketOptions.StartTls, Arg.Any<CancellationToken>());
        clock.UtcNow.Returns(Now.AddMinutes(16));
        gmail.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>()).Returns(Task.FromResult("accepted"));
        await SendAsync(service);
        factory.Received(4).Create();
    }

    [Theory]
    [InlineData("success")]
    [InlineData("timeout")]
    [InlineData("connection")]
    [InlineData("auth")]
    [InlineData("recipient")]
    [InlineData("cancel")]
    public async Task Fallback_NonQuotaOutcome_NeverCreatesSecondClient(string outcome)
    {
        var gmail = CreateClient();
        Exception? error = outcome switch
        {
            "timeout" => new TimeoutException(),
            "connection" => new IOException(),
            "auth" => new MailKit.Security.AuthenticationException(),
            "recipient" => new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, (SmtpStatusCode)550, "rejected"),
            "cancel" => new OperationCanceledException(),
            _ => null
        };
        if (error is not null)
            gmail.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>()).ThrowsAsync(error);
        var factory = CreateFactory(gmail);
        var service = CreateService(factory, FallbackOptions());
        if (error is null) await SendAsync(service);
        else await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(service));
        factory.Received(1).Create();
    }

    [Fact]
    public async Task Fallback_BrevoRejects_PropagatesFailureWithoutLoop()
    {
        var gmail = CreateClient();
        var brevo = CreateClient();
        gmail.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)550,
                "5.4.5 Daily user sending limit exceeded"));
        brevo.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .ThrowsAsync(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, (SmtpStatusCode)550, ProviderPrivateMessage));
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(gmail, brevo);
        var failure = await Assert.ThrowsAsync<EmailDeliveryException>(() => SendAsync(CreateService(factory, FallbackOptions())));
        failure.FailureKind.Should().Be(EmailDeliveryFailureKind.ProviderRejected);
        failure.ToString().Should().NotContain(ProviderPrivateMessage);
        factory.Received(2).Create();
    }

    private static EmailOptions FallbackOptions() => new()
    {
        Provider = "Smtp", SmtpHost = "smtp.gmail.com", FromEmail = "gmail@example.test",
        Username = "test", Password = "test-placeholder", EnableBrevoFallback = true,
        Brevo = new EmailOptions
        {
            SmtpHost = "smtp-relay.brevo.com", FromEmail = "brevo@example.test",
            Username = "test-brevo", Password = "test-placeholder"
        }
    };

    private static ISmtpClient CreateClient()
    {
        var client = Substitute.For<ISmtpClient>();
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(Task.FromResult("accepted"));
        return client;
    }

    private static ISmtpClientFactory CreateFactory(ISmtpClient client)
    {
        var factory = Substitute.For<ISmtpClientFactory>();
        factory.Create().Returns(client);
        return factory;
    }

    private static EmailOptions CreateOptions(string host = "smtp.gmail.com", int timeoutSeconds = 10) => new()
    {
        Provider = "Smtp",
        FromEmail = "sender@example.test",
        SmtpHost = host,
        Username = "test-user",
        Password = "test-placeholder",
        SmtpTimeoutSeconds = timeoutSeconds
    };

    private static SmtpEmailService CreateService(
        ISmtpClientFactory factory,
        EmailOptions? options = null,
        SmtpProviderCooldown? cooldown = null,
        IDateTimeProvider? clock = null,
        RecordingLogger? logger = null)
    {
        clock ??= Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return new SmtpEmailService(
            Options.Create(options ?? CreateOptions()),
            logger ?? new RecordingLogger(),
            factory,
            cooldown ?? new SmtpProviderCooldown(),
            clock);
    }

    private sealed class RecordingLogger : ILogger<SmtpEmailService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception) + exception);
    }
}

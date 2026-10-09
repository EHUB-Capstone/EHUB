using EHub.Application.Common.Exceptions;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Auth;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.ResendRegistrationOtp;
using EHub.Contracts.Auth;
using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using EHub.IntegrationTests.Common;
using EHub.Shared.Constants;
using EHub.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.IntegrationTests.Auth;

[Collection("Sequential")]
public sealed class OtpDeliveryReliabilityTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task InitialSendFailure_DoesNotLeaveAccountOrPendingChallenge()
    {
        var request = Request();
        var sender = new TestSender { Failure = new EmailDeliveryException(EmailDeliveryFailureKind.QuotaExceeded) };
        var result = await Register(request, sender);
        Assert.Equal(AuthErrors.EmailTemporarilyUnavailable.Code, result.Error.Code);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.PendingRegistrations.AnyAsync(row => row.NormalizedEmail == request.Email));
        Assert.False(await db.Users.AnyAsync(row => row.NormalizedEmail == request.Email));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedRetry_PreservesDeliveredOtpAndCounters(bool retryThroughRegister)
    {
        var request = Request();
        var pending = await SeedDeliveredChallenge(request);
        var sender = new TestSender { Failure = new EmailDeliveryException(EmailDeliveryFailureKind.QuotaExceeded) };
        var result = retryThroughRegister
            ? await Register(request, sender, "135790")
            : await Resend(pending.Id, sender, "135790");
        Assert.Equal(AuthErrors.EmailTemporarilyUnavailable.Code, result.Error.Code);
        var persisted = await Read(pending.Id);
        Assert.Equal(pending.OtpHash, persisted.OtpHash);
        Assert.Equal(pending.OtpExpiresAtUtc, persisted.OtpExpiresAtUtc);
        Assert.Equal(pending.LastSentAtUtc, persisted.LastSentAtUtc);
        Assert.Equal(pending.ResendCount, persisted.ResendCount);
        Assert.Equal(pending.FailedAttemptCount, persisted.FailedAttemptCount);
        Assert.Equal(pending.PasswordHash, persisted.PasswordHash);
        using var scope = factory.Services.CreateScope();
        Assert.True(scope.ServiceProvider.GetRequiredService<IRegistrationOtpService>()
            .VerifyCode(pending.Id, "246810", persisted.OtpHash));
    }

    [Fact]
    public async Task SuccessfulResend_RotatesOtpAndCountsOnlyAcceptedSend()
    {
        var pending = await SeedDeliveredChallenge(Request());
        var result = await Resend(pending.Id, new TestSender(), "135790");
        Assert.True(result.IsSuccess);
        var persisted = await Read(pending.Id);
        Assert.Equal(pending.ResendCount + 1, persisted.ResendCount);
        Assert.NotEqual(pending.OtpHash, persisted.OtpHash);
        using var scope = factory.Services.CreateScope();
        var otp = scope.ServiceProvider.GetRequiredService<IRegistrationOtpService>();
        Assert.True(otp.VerifyCode(pending.Id, "135790", persisted.OtpHash));
        Assert.False(otp.VerifyCode(pending.Id, "246810", persisted.OtpHash));
    }

    [Fact]
    public async Task CancellationDuringSend_RollsBackChallenge()
    {
        var pending = await SeedDeliveredChallenge(Request());
        using var cancellation = new CancellationTokenSource();
        var sender = new TestSender
        {
            OnSend = ct => { cancellation.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Resend(pending.Id, sender, "135790", cancellation.Token));
        var persisted = await Read(pending.Id);
        Assert.Equal(pending.OtpHash, persisted.OtpHash);
        Assert.Equal(pending.ResendCount, persisted.ResendCount);
    }

    [Fact]
    public async Task CompetingResends_DoNotSendTwoReplacementCodes()
    {
        var pending = await SeedDeliveredChallenge(Request());
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new TestSender
        {
            OnSend = async ct => { sending.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(10), ct); }
        };
        var first = Resend(pending.Id, sender, "135790");
        await sending.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Resend(pending.Id, sender, "975310");
        release.TrySetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.IsFailure && result.Error.Code == AuthErrors.VerificationResendTooSoon.Code);
        Assert.Equal(1, sender.Calls);
    }

    private async Task<PendingRegistration> SeedDeliveredChallenge(RegisterRequest request)
    {
        var result = await Register(request, new TestSender());
        Assert.True(result.IsSuccess);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = await db.PendingRegistrations.SingleAsync(row => row.NormalizedEmail == request.Email);
        pending.LastSentAtUtc = DateTime.UtcNow.AddMinutes(-2);
        pending.FailedAttemptCount = 1;
        await db.SaveChangesAsync();
        return await Read(pending.Id);
    }

    private async Task<PendingRegistration> Read(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .PendingRegistrations.AsNoTracking().SingleAsync(row => row.Id == id);
    }

    private async Task<Result<RegisterResult>> Register(RegisterRequest request, TestSender sender, string code = "246810")
    {
        using var scope = factory.Services.CreateScope();
        var otp = new FixedCodeOtp(scope.ServiceProvider.GetRequiredService<IRegistrationOtpService>(), code);
        var handler = ActivatorUtilities.CreateInstance<RegisterCommandHandler>(scope.ServiceProvider, sender, otp);
        return await handler.HandleAsync(request);
    }

    private async Task<Result<RegisterResult>> Resend(Guid id, TestSender sender, string code, CancellationToken ct = default)
    {
        using var scope = factory.Services.CreateScope();
        var otp = new FixedCodeOtp(scope.ServiceProvider.GetRequiredService<IRegistrationOtpService>(), code);
        var handler = ActivatorUtilities.CreateInstance<ResendRegistrationOtpCommandHandler>(scope.ServiceProvider, sender, otp);
        return await handler.HandleAsync(new ResendRegistrationOtpRequest { RegistrationId = id }, ct);
    }

    private static RegisterRequest Request() => new()
    {
        FullName = "OTP Test", Email = $"otp-{Guid.NewGuid():N}@example.com",
        Password = "TestPassword123", ConfirmPassword = "TestPassword123",
        Role = SystemRoles.Student, MajorCode = MajorCodes.BIT_SE
    };

    private sealed class FixedCodeOtp(IRegistrationOtpService inner, string code) : IRegistrationOtpService
    {
        public string GenerateCode() => code;
        public string HashCode(Guid id, string value) => inner.HashCode(id, value);
        public bool VerifyCode(Guid id, string value, string hash) => inner.VerifyCode(id, value, hash);
    }

    private sealed class TestSender : IEmailService
    {
        private int _calls;
        public int Calls => _calls;
        public Exception? Failure { get; init; }
        public Func<CancellationToken, Task>? OnSend { get; init; }
        public async Task SendRegistrationOtpAsync(string toEmail, string fullName, string otp, DateTime expiresAtUtc, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _calls);
            if (OnSend is not null) await OnSend(ct);
            if (Failure is not null) throw Failure;
        }
        public Task SendPasswordResetEmailAsync(string email, string name, string url, DateTime expiry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SendPasswordChangedNotificationAsync(string email, string name, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SendClassNotificationAsync(string email, string name, string subject, string title, string message, CancellationToken ct = default) => throw new NotSupportedException();
    }
}

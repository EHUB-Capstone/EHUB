using EHub.Application.Common.Exceptions;

namespace EHub.Infrastructure.Services.Email;

/// <summary>
/// Shared by all email sends in this process. This protects a known-exhausted SMTP
/// account from repeated attempts; it does not track or increase provider quota.
/// </summary>
public sealed class SmtpProviderCooldown
{
    private readonly object _sync = new();
    private DateTime? _retryAfterUtc;

    public void ThrowIfActive(DateTime utcNow)
    {
        lock (_sync)
        {
            if (_retryAfterUtc > utcNow)
            {
                throw new EmailDeliveryException(EmailDeliveryFailureKind.QuotaExceeded, _retryAfterUtc);
            }
        }
    }

    public DateTime RecordQuotaFailure(DateTime utcNow, TimeSpan cooldown)
    {
        lock (_sync)
        {
            var nextRetryUtc = utcNow.Add(cooldown);
            if (!_retryAfterUtc.HasValue || nextRetryUtc > _retryAfterUtc.Value)
            {
                _retryAfterUtc = nextRetryUtc;
            }

            return _retryAfterUtc.Value;
        }
    }
}

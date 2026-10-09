namespace EHub.Application.Common.Exceptions;

public enum EmailDeliveryFailureKind
{
    Configuration,
    QuotaExceeded,
    Authentication,
    Tls,
    Connection,
    Timeout,
    SenderRejected,
    RecipientRejected,
    ProviderRejected,
    Unknown
}

/// <summary>
/// Safe email failure details. Provider responses and inner exceptions may contain
/// addresses or credentials, so they must not cross the email service boundary.
/// </summary>
public sealed class EmailDeliveryException : Exception
{
    public EmailDeliveryException(EmailDeliveryFailureKind failureKind, DateTime? retryAfterUtc = null)
        : base($"Email delivery failed: {failureKind}.")
    {
        FailureKind = failureKind;
        RetryAfterUtc = retryAfterUtc;
    }

    public EmailDeliveryFailureKind FailureKind { get; }

    /// <summary>The next local retry time, not a promise that provider quota has reset.</summary>
    public DateTime? RetryAfterUtc { get; }
}

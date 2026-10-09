# Email delivery and retry behavior

E-HUB uses the configured SMTP provider through MailKit. SMTP acceptance is not
proof of inbox delivery; a later bounce remains possible.

After DATA acceptance, the client closes locally without waiting for a network
QUIT reply. Safe stage-duration logs measure connect/authenticate/send latency;
they contain no recipient, message body, OTP or credentials. This removes a cleanup
round trip, not the provider's delivery latency. Measure before/after on live traffic.

## Registration OTP

Registration and resend save their new challenge inside a serializable database
transaction before sending, and commit only after SMTP acceptance. Explicit send
failure rolls back the replacement hash, expiration and resend count. A failed
initial registration leaves no new pending challenge. Concurrent requests conflict
before a second send or observe the committed cooldown. Transactions are never
automatically retried after sending mail. HTTP rate limiting remains enabled.

The SMTP operation defaults to a 10-second total timeout. This bounds the time a
registration transaction holds database locks; measure contention under load.

## SMTP failures

The service emits safe categories instead of raw SMTP exception content:
Configuration, QuotaExceeded, Authentication, Tls, Connection, Timeout,
SenderRejected, RecipientRejected, ProviderRejected, Unknown.

Explicit Gmail `5.4.5 Daily user sending limit exceeded` opens a process-local
15-minute cooldown. New sends fail fast during this period. This is a retry delay,
not a provider quota reset and not a distributed limit across app replicas.
Registration returns HTTP 503 with `AUTH_EMAIL_TEMPORARILY_UNAVAILABLE` for this
case; other send failures retain `AUTH_EMAIL_DELIVERY_FAILED`.

Optional configuration (no credentials):

- `Email:SmtpTimeoutSeconds`: default 10, accepted range 1–120.
- `Email:QuotaCooldownMinutes`: default 15, accepted range 1–1440.

Keep timeout below the caller's HTTP timeout. Credentials remain in User Secrets
locally or environment/secret storage in deployment. Never enable SMTP protocol
logging with credentials or OTP content.

## Optional Gmail-first, Brevo fallback

Keep the existing `Email` SMTP settings pointed at Gmail. Configure the following
in backend User Secrets (local) or deployment secret storage, never in frontend:

| Key | Value |
| --- | --- |
| `Email:EnableBrevoFallback` | `true` (default is false) |
| `Email:Brevo:SmtpHost` | `smtp-relay.brevo.com` |
| `Email:Brevo:SmtpPort` | `587` |
| `Email:Brevo:SecureSocketOption` | `StartTls` |
| `Email:Brevo:FromName` | `EHUB` |
| `Email:Brevo:FromEmail` | Your verified Brevo sender |
| `Email:Brevo:Username` | Brevo SMTP Login (not account email) |
| `Email:Brevo:Password` | Brevo SMTP Key (not API Key) |

Restart the backend after configuring. Do not paste credentials into chat or logs.
This change does not provision credentials or enable fallback automatically.
Only explicit Gmail daily-limit rejection or its active cooldown triggers Brevo.
The same OTP/content is used, with the Brevo sender address. After Gmail cooldown
expires, requests try Gmail first again. No fallback occurs for timeout, connection,
authentication, recipient errors, cancellation, or failures after SMTP acceptance.
Brevo failure propagates normally; there is no loop back to Gmail and no shared
Gmail cooldown applied to Brevo. Brevo acceptance can still mean queueing rather
than immediate delivery; provider quota and inbox delivery are not guaranteed.
Two attempts can take up to the sum of both SMTP timeouts (20 seconds by default),
plus bounded cleanup. Keep this below the HTTP timeout and measure database locks.

## Class notification outbox

Class events fan out into one durable email outbox event per recipient. The event
ID is deterministic for the source event and normalized recipient. Notifications,
fan-out and parent completion commit atomically in the outbox processor. Retrying
one failed recipient does not repeat already processed sibling deliveries.
Quota failures postpone work until the cooldown without exhausting the delivery
retry count; other failures use the existing bounded backoff.

Already failed legacy outbox events are not automatically requeued by this change.
Review them before manually replaying: a legacy batch may have partially sent.

## Limits of SMTP atomicity

SMTP and PostgreSQL cannot commit atomically. A process crash or database commit
failure after SMTP acceptance can still leave an email with an uncommitted OTP,
or cause an outbox recipient to be sent twice on retry. Network timeouts may also
leave acceptance uncertain. The new code prevents ordinary rejection rollback
bugs and batch replays, but does not claim exactly-once delivery. Cleanup failures
after acceptance are logged safely and do not trigger a second send.

Automated tests use fake SMTP clients/senders and an isolated PostgreSQL container.
They do not consume Gmail quota or prove that a real mailbox received an email.

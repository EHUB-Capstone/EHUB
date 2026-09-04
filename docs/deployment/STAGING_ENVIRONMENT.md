# EHUB staging deployment configuration

This document lists configuration keys only. Never commit real passwords,
tokens, or database connection strings.

## Architecture

- Vercel serves the Vite SPA.
- Vercel rewrites same-origin `/api/*` requests to Render.
- Render runs the ASP.NET Core Docker image on port `10000`.
- Neon provides PostgreSQL.

## Render runtime variables

Plain configuration:

```text
ASPNETCORE_ENVIRONMENT=Staging
ASPNETCORE_URLS=http://+:10000
ReverseProxy__Enabled=true
Jwt__Issuer=EHub.Api.Staging
Jwt__Audience=EHub.Web.Staging
Jwt__AccessTokenExpirationMinutes=30
Jwt__RefreshTokenExpirationDays=7
Google__ClientId=<google-web-client-id>
Cors__AllowedOrigins__0=https://<stable-vercel-domain>
Frontend__BaseUrl=https://<stable-vercel-domain>
PasswordReset__TokenExpirationMinutes=15
Email__Provider=Smtp
Email__FromName=EHUB
Email__FromEmail=<verified-sender-email>
Email__SmtpHost=<smtp-host>
Email__SmtpPort=587
Email__SecureSocketOption=StartTls
RegistrationOtp__ExpirationMinutes=5
RegistrationOtp__MaximumAttempts=5
RegistrationOtp__ResendCooldownSeconds=60
RegistrationOtp__MaximumResends=5
RegistrationOtp__CleanupRetentionHours=24
```

Secrets:

```text
ConnectionStrings__DefaultConnection=<neon-pooled-npgsql-connection-string>
Jwt__Secret=<random-secret-at-least-32-characters>
RegistrationOtp__HashKey=<separate-random-secret-at-least-32-characters>
Email__Username=<smtp-username>
Email__Password=<smtp-password-or-api-key>
```

Use the direct Neon connection string when applying migrations and the pooled
connection string for the running API.

## Vercel build variables

```text
API_PROXY_ORIGIN=https://<render-service-domain>.onrender.com
VITE_GOOGLE_CLIENT_ID=<google-web-client-id>
VITE_ENABLE_API_MOCKS=false
VITE_ENABLE_REALTIME=false
VITE_SHOW_UNAVAILABLE_CLASS_FEATURES=false
VITE_FEATURE_CLASS_RENAME=false
VITE_FEATURE_CLASS_LIFECYCLE=true
VITE_FEATURE_CLASS_MAJOR_VERIFICATION=true
VITE_FEATURE_CLASS_MENTOR_ASSIGNMENT=true
VITE_FEATURE_CLASS_TEAM_MANAGEMENT=true
VITE_FEATURE_CLASS_CHAT_BACKFILL=true
VITE_FEATURE_CLASS_PROJECT_DIRECTION=true
VITE_FEATURE_CLASS_STUDENT_SELF_SERVICE=true
VITE_FEATURE_CLASS_LECTURER_STUDENT_IMPORT=true
```

Set every class feature flag explicitly after its backend contract and
permissions have passed staging verification. Do not store secrets in any
`VITE_*` variable because Vite embeds those values in the browser bundle.

## One-time database initialization

Run from a trusted local machine with the direct Neon connection string and
the `AdminSeed` values supplied through environment variables or .NET user
secrets:

```text
dotnet run --project backend/src/EHub.Api/EHub.Api.csproj -- --initialize-database
```

The command applies EF Core migrations, runs idempotent reference-data seeders,
creates the configured admin when absent, and exits without starting the API.
Do not configure the deployed service to run this command on every startup.

## Health endpoints

- `/health/live`: process liveness only; use for the Render health check.
- `/health/ready`: includes PostgreSQL connectivity; use for deployment checks.
- `/health`: backwards-compatible aggregate readiness endpoint.

## Realtime limitation

The frontend contains optional Socket.IO consumers, but this repository does
not currently expose a compatible realtime backend. Keep
`VITE_ENABLE_REALTIME=false` for mentor staging until such a service exists.
REST chat history remains available, while realtime send/presence operations
are reported as unavailable instead of repeatedly calling a nonexistent host.

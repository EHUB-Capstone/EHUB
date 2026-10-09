using Microsoft.Extensions.Hosting;

namespace EHub.Api.Extensions;

public static class RuntimeConfigurationValidationExtensions
{
    public static void ValidateRuntimeConfiguration(
        this IConfiguration configuration,
        IHostEnvironment environment)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required.");
        }

        if (environment.IsDevelopment())
        {
            return;
        }

        var frontendBaseUrl = configuration["Frontend:BaseUrl"];
        if (!Uri.TryCreate(frontendBaseUrl, UriKind.Absolute, out var frontendUri) ||
            frontendUri.Scheme != Uri.UriSchemeHttps ||
            frontendUri.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(frontendUri.Query) ||
            !string.IsNullOrEmpty(frontendUri.Fragment))
        {
            throw new InvalidOperationException(
                "Frontend:BaseUrl must be an absolute HTTPS origin without a path outside Development.");
        }

        var googleClientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId) ||
            googleClientId.StartsWith("replace-with-", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Google:ClientId must be configured outside Development.");
        }

        var otpHashKey = configuration["RegistrationOtp:HashKey"];
        if (string.IsNullOrWhiteSpace(otpHashKey) || otpHashKey.Length < 32)
        {
            throw new InvalidOperationException(
                "RegistrationOtp:HashKey must contain at least 32 characters outside Development.");
        }

        if (!string.Equals(configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Email:Provider must be Smtp outside Development so registration codes can be delivered.");
        }

        var requiredEmailKeys = new[]
        {
            "Email:FromEmail",
            "Email:SmtpHost",
            "Email:Username",
            "Email:Password"
        };
        foreach (var key in requiredEmailKeys)
        {
            if (string.IsNullOrWhiteSpace(configuration[key]))
            {
                throw new InvalidOperationException($"{key} is required when Email:Provider is Smtp.");
            }
        }
    }
}

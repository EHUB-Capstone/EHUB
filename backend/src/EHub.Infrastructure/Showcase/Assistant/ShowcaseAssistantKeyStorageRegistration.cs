using EHub.Application.Common.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EHub.Infrastructure.Showcase.Assistant;

public static class ShowcaseAssistantKeyStorageRegistration
{
    /// <summary>
    /// Lets users keep their own Gemini key on the server, encrypted. It stays off until an encryption key is configured, and a
    /// malformed or partial configuration stops the application at start instead of silently turning the protection off. The
    /// encryption keys only ever come from configuration (environment or secret store): never from the database or the repository.
    /// </summary>
    public static IServiceCollection AddShowcaseAssistantKeyStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ShowcaseAssistantKeyEncryptionOptions>().Bind(configuration.GetSection(ShowcaseAssistantKeyEncryptionOptions.SectionName))
            .Validate(options => options.IsValid,
                "Showcase:Assistant:KeyEncryption needs ActiveKeyId (1-255) and Keys with a Base64 256-bit key for that id, or must be left empty.")
            .ValidateOnStart();
        services.AddSingleton<IShowcaseAssistantKeyProtector, AesGcmShowcaseAssistantKeyProtector>();
        return services;
    }
}

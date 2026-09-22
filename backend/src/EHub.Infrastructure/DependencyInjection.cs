using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Infrastructure.Persistence;
using EHub.Infrastructure.Persistence.Repositories;
using EHub.Infrastructure.Identity;
using EHub.Infrastructure.Services;
using EHub.Infrastructure.Services.Email;
using EHub.Infrastructure.Services.Auth;
using EHub.Application.Common.Models.Identity;
using EHub.Infrastructure.Options;
using EHub.Infrastructure.BackgroundJobs;
using CloudinaryDotNet;
using EHub.Application.Common.Interfaces.Storage;
using EHub.Infrastructure.Storage;
using EHub.Application.Common.Interfaces.AI;
using Microsoft.Extensions.Hosting;

namespace EHub.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                               configuration["ConnectionStrings:DefaultConnection"] ??
                               configuration["ConnectionStrings__DefaultConnection"];

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString,
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<IAiFeatureGate, ConfigurationAiFeatureGate>();
        services.AddSingleton<MockProposalAnalysisProvider>();
        services.AddSingleton<ProposalAnalysisPromptBuilder>();
        services.AddHttpClient<GeminiProposalAnalysisProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProposalAnalysisOptions>>().Value;
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 180));
        });
        services.AddScoped<IProposalAnalysisProvider>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProposalAnalysisOptions>>().Value;
            return options.Provider.Trim().ToLowerInvariant() switch
            {
                "mock" => provider.GetRequiredService<MockProposalAnalysisProvider>(),
                "gemini" => provider.GetRequiredService<GeminiProposalAnalysisProvider>(),
                _ => throw new InvalidOperationException("AI:Analysis:Provider must be Mock or Gemini.")
            };
        });
        services.AddSingleton<DeterministicLocalEmbeddingProvider>();
        services.AddHttpClient<GeminiEmbeddingProvider>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProposalEmbeddingOptions>>().Value;
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        });
        services.AddScoped<IEmbeddingProvider>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ProposalEmbeddingOptions>>().Value;
            return options.Provider.Trim().ToLowerInvariant() switch
            {
                "deterministiclocal" => provider.GetRequiredService<DeterministicLocalEmbeddingProvider>(),
                "gemini" => provider.GetRequiredService<GeminiEmbeddingProvider>(),
                _ => throw new InvalidOperationException("AI:Embedding:Provider must be DeterministicLocal or Gemini.")
            };
        });
        services.AddSingleton<ProjectProposalAnalysisWorker>();
        services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ProjectProposalAnalysisWorker>());
        services.AddHostedService<ClassImportSessionCleanupService>();
        services.AddHostedService<LecturerImportSessionCleanupService>();
        services.AddHostedService<PendingRegistrationCleanupService>();
        services.AddScoped<IOutboxEventDispatcher, NotificationOutboxEventDispatcher>();
        services.AddScoped<IClassChatMembershipSynchronizer, ClassChatMembershipSynchronizer>();
        services.AddSingleton<ProjectDirectionRealtimeService>();
        services.AddSingleton<IProjectDirectionRealtimePublisher>(provider =>
            provider.GetRequiredService<ProjectDirectionRealtimeService>());
        services.AddSingleton<ICheckpointFeedbackRealtimePublisher>(provider =>
            provider.GetRequiredService<ProjectDirectionRealtimeService>());
        services.AddSingleton<IClassRealtimePublisher>(provider =>
            provider.GetRequiredService<ProjectDirectionRealtimeService>());
        services.AddHostedService<OutboxProcessorBackgroundService>();

        // Repositories & Persistence
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IMentorProfileRepository, MentorProfileRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Identity Services
        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Common Services
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<IPasswordResetTokenService, PasswordResetTokenService>();
        services.AddSingleton<IRegistrationOtpService, RegistrationOtpService>();
        services.AddOptions<CloudinaryOptions>()
            .Bind(configuration.GetSection(CloudinaryOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.CloudName), "Cloudinary:CloudName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiKey), "Cloudinary:ApiKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ApiSecret), "Cloudinary:ApiSecret is required.")
            .ValidateOnStart();
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CloudinaryOptions>>().Value;
            return new Cloudinary(new Account(options.CloudName, options.ApiKey, options.ApiSecret));
        });
        services.AddScoped<IImageStorageService, CloudinaryImageStorageService>();
        services.AddHttpClient("CloudinarySubmissionFiles", client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddScoped<ISubmissionFileStorageService, CloudinarySubmissionFileStorageService>();
        
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var emailProvider = configuration["Email:Provider"];
        if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISmtpClientFactory, SmtpClientFactory>();
            services.AddSingleton<SmtpProviderCooldown>();
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, ConsoleEmailService>();
        }

        // HTTP Context Accessor
        services.AddHttpContextAccessor();

        // Configuration Options Binding
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<PasswordResetOptions>(configuration.GetSection(PasswordResetOptions.SectionName));
        services.AddOptions<ProposalEmbeddingOptions>()
            .Bind(configuration.GetSection(ProposalEmbeddingOptions.SectionName));
        services.AddOptions<ProposalAnalysisOptions>()
            .Bind(configuration.GetSection(ProposalAnalysisOptions.SectionName));
        services.AddOptions<RegistrationOtpOptions>()
            .Bind(configuration.GetSection(RegistrationOtpOptions.SectionName))
            .Validate(options => options.ExpirationMinutes is >= 1 and <= 15,
                "RegistrationOtp:ExpirationMinutes must be between 1 and 15.")
            .Validate(options => options.MaximumAttempts is >= 3 and <= 10,
                "RegistrationOtp:MaximumAttempts must be between 3 and 10.")
            .Validate(options => options.ResendCooldownSeconds is >= 30 and <= 300,
                "RegistrationOtp:ResendCooldownSeconds must be between 30 and 300.")
            .Validate(options => options.MaximumResends is >= 1 and <= 10,
                "RegistrationOtp:MaximumResends must be between 1 and 10.")
            .Validate(options => options.CleanupRetentionHours is >= 1 and <= 168,
                "RegistrationOtp:CleanupRetentionHours must be between 1 and 168.")
            .ValidateOnStart();

        return services;
    }
}

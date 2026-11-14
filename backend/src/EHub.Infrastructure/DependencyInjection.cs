using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Application.Common.Interfaces.Identity;
using EHub.Application.Common.Interfaces.Services;
using EHub.Application.Features.Showcase.Upvote;
using EHub.Application.Features.Showcase.Views;
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
using EHub.Infrastructure.Mentoring;
using EHub.Infrastructure.Mentoring.Gemini;
using EHub.Application.Features.Mentoring.Scoring;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;

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
                b => { b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName); b.UseVector(); }));

        var matchingProvider = configuration["Mentoring:MatchingProvider"] ?? "Gemini";
        if (!string.Equals(matchingProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mentoring:MatchingProvider supports only Gemini. Configure the current AI service before starting.");
        services.AddOptions<MentorScoringOptions>().Bind(configuration.GetSection(MentorScoringOptions.SectionName))
            .Validate(options => options.IsValid(), "Mentoring:Scoring requires criterion weights from 0 to 100 with a positive total.")
            .ValidateOnStart();
        services.AddSingleton<IMentorEvidenceGuard>(new FileMentorEvidenceGuard(configuration));
        services.AddOptions<GeminiMentorOptions>().Bind(configuration.GetSection(GeminiMentorOptions.SectionName))
            .Validate(options => options.IsValid(),
                "Configure Mentoring:Gemini with a server-side API key, reviewed professional-data transfer enabled and approved model IDs.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<GeminiRequestCoordinator>();
        services.AddHttpClient<IGenerativeMentorFitRanker, GeminiMentorFitRanker>(client =>
            client.Timeout = TimeSpan.FromSeconds(55))
            .RedactLoggedHeaders(["x-goog-api-key"])
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        AddCheckpointSummaries(services, configuration);

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IShowcaseProjectSearch, ShowcaseProjectSearch>();
        services.AddScoped<IShowcaseUpvoteStore, ShowcaseUpvoteStore>();
        services.AddScoped<IShowcaseViewStore, ShowcaseViewStore>();
        services.AddHostedService<ClassImportSessionCleanupService>();
        services.AddHostedService<LecturerImportSessionCleanupService>();
        services.AddHostedService<MentorAdminSessionCleanupService>();
        services.AddHostedService<MentorAllocationJobService>();
        services.AddHostedService<PendingRegistrationCleanupService>();
        services.AddHostedService<PendingSubmissionUploadCleanupService>();
        services.AddHostedService<SubmissionPreviewGenerationService>();
        services.AddHostedService<TeamFormationInvitationExpiryService>();
        services.AddScoped<IOutboxEventDispatcher, NotificationOutboxEventDispatcher>();
        services.AddSingleton<IOutboxWakeSignal, OutboxWakeSignal>();
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
        services.AddScoped<IMentorDocumentStorageService, CloudinaryMentorDocumentStorageService>();
        services.AddScoped<IMentorCvTextReader, MentorCvTextReader>();
        services.AddOptions<R2Options>()
            .Bind(configuration.GetSection(R2Options.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccountId), "R2:AccountId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AccessKeyId), "R2:AccessKeyId is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SecretAccessKey), "R2:SecretAccessKey is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.BucketName), "R2:BucketName is required.")
            .ValidateOnStart();
        services.AddSingleton<ISubmissionObjectStorage, R2SubmissionObjectStorage>();
        services.AddOptions<DocumentPreviewOptions>()
            .Bind(configuration.GetSection(DocumentPreviewOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.LibreOfficeExecutablePath),
                "DocumentPreview:LibreOfficeExecutablePath is required.")
            .Validate(options => options.ConversionTimeoutSeconds is >= 10 and <= 300,
                "DocumentPreview:ConversionTimeoutSeconds must be between 10 and 300.")
            .Validate(options => options.MaximumConcurrentConversions is >= 1 and <= 4,
                "DocumentPreview:MaximumConcurrentConversions must be between 1 and 4.")
            .ValidateOnStart();
        services.AddSingleton<IDocumentPreviewConverter, LibreOfficeDocumentPreviewConverter>();
        
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

    /// <summary>
    /// AI checkpoint summaries run on a local Ollama model only. <c>Summarization:Provider</c> is Disabled by default;
    /// the queue worker is registered only when it is Ollama.
    /// </summary>
    private static void AddCheckpointSummaries(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Summarization:Provider"] ?? "Disabled";
        var enabled = string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase);
        if (!enabled && !string.Equals(provider, "Disabled", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Summarization:Provider must be Disabled or Ollama.");
        var maxInput = configuration.GetValue("Summarization:MaxInputCharacters", 30_000);
        var filePrompt = configuration.GetValue("Summarization:FilePromptCharacters", 5_000);
        var contextLength = configuration.GetValue("Summarization:ContextLength", 4096);
        var timeoutMinutes = configuration.GetValue("Summarization:JobTimeoutMinutes", 10);
        if (maxInput is < 1_000 or > 100_000)
            throw new InvalidOperationException("Summarization:MaxInputCharacters must be between 1000 and 100000.");
        // About 3 characters per token, plus about 1,200 tokens for the instructions, checklist and answer.
        if (filePrompt is < 2_000 or > 18_000 || filePrompt > maxInput || filePrompt / 3 + 1_200 > contextLength)
            throw new InvalidOperationException(
                "Summarization:FilePromptCharacters must be between 2000 and 18000, not above MaxInputCharacters, and fit ContextLength.");
        if (timeoutMinutes is < 1 or > 14)
            throw new InvalidOperationException("Summarization:JobTimeoutMinutes must be between 1 and 14 (the processing lease is 15).");

        services.AddSingleton(new EHub.Application.Features.Workspaces.CheckpointSummaries.CheckpointSummarySettings
        {
            Enabled = enabled, AutoSummarize = configuration.GetValue("Summarization:AutoSummarize", true),
            MaxInputCharacters = maxInput, FilePromptCharacters = filePrompt,
            JobTimeout = TimeSpan.FromMinutes(timeoutMinutes)
        });
        services.AddSingleton<ISubmissionTextExtractor, EHub.Infrastructure.Summarization.SubmissionTextExtractor>();
        services.AddSingleton<ICheckpointSummaryQueueSignal, CheckpointSummaryQueueSignal>();
        // Reuse the data-only alias catalog; checkpoint summaries do not depend on a mentor scoring model.
        services.AddSingleton(_ => new EHub.Application.Features.Workspaces.CheckpointSummaries.CheckpointSummaryTopics(
            new FileMentorEvidenceGuard(configuration).FindAffirmedTopics));
        services.AddSingleton<ICheckpointSummaryCancellation, CheckpointSummaryCancellation>();
        // One per instance: it remembers whether the model has been answering, so the worker can pause while it is down.
        services.AddSingleton<EHub.Application.Features.Workspaces.CheckpointSummaries.CheckpointSummaryCircuitBreaker>();
        if (!enabled)
        {
            services.AddSingleton<ICheckpointSummarizer, EHub.Infrastructure.Summarization.DisabledCheckpointSummarizer>();
            return;
        }

        var baseUrl = configuration["Summarization:BaseUrl"] ?? "http://localhost:11434";
        services.AddHttpClient<ICheckpointSummarizer, EHub.Infrastructure.Summarization.OllamaCheckpointSummarizer>(client =>
        {
            client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/", UriKind.Absolute);
            // The job timeout bounds a whole summary; this only stops a single stuck call.
            client.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseProxy = false });
        services.AddHostedService<CheckpointSummaryGenerationService>();
        services.AddHostedService<CheckpointSummaryPreviewService>();
    }
}

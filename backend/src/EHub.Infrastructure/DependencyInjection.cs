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
        services.AddHostedService<ClassImportSessionCleanupService>();
        services.AddHostedService<LecturerImportSessionCleanupService>();
        services.AddHostedService<PendingRegistrationCleanupService>();
        services.AddScoped<IOutboxEventDispatcher, NotificationOutboxEventDispatcher>();
        services.AddScoped<IClassChatMembershipSynchronizer, ClassChatMembershipSynchronizer>();
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
        
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var emailProvider = configuration["Email:Provider"];
        if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
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
}

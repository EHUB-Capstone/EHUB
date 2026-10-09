using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using EHub.Shared.Constants;

namespace EHub.Api.Extensions;

public static class AuthorizationExtensions
{
    public static IServiceCollection AddApplicationAuthorization(
        this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(SystemPolicies.AuthenticatedOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
            });

            options.AddPolicy(SystemPolicies.AdminOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRoles.Admin);
            });

            options.AddPolicy(SystemPolicies.StudentOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRoles.Student);
            });

            options.AddPolicy(SystemPolicies.LecturerOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRoles.Lecturer);
            });

            options.AddPolicy(SystemPolicies.MentorOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRoles.Mentor);
            });

            options.AddPolicy(SystemPolicies.StaffOnly, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(SystemRoles.Admin, SystemRoles.Lecturer);
            });
        });

        return services;
    }
}

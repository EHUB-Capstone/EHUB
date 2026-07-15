using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.Login;
using EHub.Application.Features.Auth.GoogleLogin;
using EHub.Application.Features.Auth.GetCurrentUser;
using EHub.Application.Features.Auth.RefreshToken;
using EHub.Application.Features.Auth.Logout;
using EHub.Application.Features.Admin.Users.GetPendingApprovalUsers;
using EHub.Application.Features.Admin.Users.ApproveUser;
using EHub.Application.Features.Admin.Users.RejectUser;

namespace EHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IRegisterCommandHandler, RegisterCommandHandler>();
        services.AddScoped<ILoginCommandHandler, LoginCommandHandler>();
        services.AddScoped<IGoogleLoginCommandHandler, GoogleLoginCommandHandler>();
        services.AddScoped<IGetCurrentUserQueryHandler, GetCurrentUserQueryHandler>();
        services.AddScoped<IRefreshTokenCommandHandler, RefreshTokenCommandHandler>();
        services.AddScoped<ILogoutCommandHandler, LogoutCommandHandler>();

        services.AddScoped<IGetPendingApprovalUsersQueryHandler, GetPendingApprovalUsersQueryHandler>();
        services.AddScoped<IApproveUserCommandHandler, ApproveUserCommandHandler>();
        services.AddScoped<IRejectUserCommandHandler, RejectUserCommandHandler>();

        services.AddScoped<EHub.Application.Common.Interfaces.Authorization.IPermissionService, EHub.Application.Common.Services.Authorization.PermissionService>();

        return services;
    }
}

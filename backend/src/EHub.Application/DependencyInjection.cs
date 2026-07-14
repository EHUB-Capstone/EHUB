using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.Login;
using EHub.Application.Features.Auth.GoogleLogin;

namespace EHub.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddScoped<IRegisterCommandHandler, RegisterCommandHandler>();
        services.AddScoped<ILoginCommandHandler, LoginCommandHandler>();
        services.AddScoped<IGoogleLoginCommandHandler, GoogleLoginCommandHandler>();

        return services;
    }
}

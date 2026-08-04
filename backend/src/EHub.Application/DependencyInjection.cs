using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using System.Reflection;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.Login;
using EHub.Application.Features.Auth.GoogleLogin;
using EHub.Application.Features.Auth.GetCurrentUser;
using EHub.Application.Features.Auth.RefreshToken;
using EHub.Application.Features.Auth.Logout;
using EHub.Application.Features.Auth.ForgotPassword;
using EHub.Application.Features.Auth.ResetPassword;
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
        services.AddScoped<IForgotPasswordCommandHandler, ForgotPasswordCommandHandler>();
        services.AddScoped<IResetPasswordCommandHandler, ResetPasswordCommandHandler>();

        services.AddScoped<IGetPendingApprovalUsersQueryHandler, GetPendingApprovalUsersQueryHandler>();
        services.AddScoped<IApproveUserCommandHandler, ApproveUserCommandHandler>();
        services.AddScoped<IRejectUserCommandHandler, RejectUserCommandHandler>();

        services.AddScoped<EHub.Application.Features.Classes.GetClasses.IGetClassesQueryHandler, EHub.Application.Features.Classes.GetClasses.GetClassesQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.CreateClass.ICreateClassCommandHandler, EHub.Application.Features.Classes.CreateClass.CreateClassCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.CreateBulkClasses.ICreateBulkClassesCommandHandler, EHub.Application.Features.Classes.CreateBulkClasses.CreateBulkClassesCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.UpdateClass.IUpdateClassCommandHandler, EHub.Application.Features.Classes.UpdateClass.UpdateClassCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.UpdateClassSchedule.IUpdateClassScheduleCommandHandler, EHub.Application.Features.Classes.UpdateClassSchedule.UpdateClassScheduleCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.GetClassDetail.IGetClassDetailQueryHandler, EHub.Application.Features.Classes.GetClassDetail.GetClassDetailQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.GetClassRoster.IGetClassRosterQueryHandler, EHub.Application.Features.Classes.GetClassRoster.GetClassRosterQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.AddStudentToClass.IAddStudentToClassCommandHandler, EHub.Application.Features.Classes.AddStudentToClass.AddStudentToClassCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.UpdateClassStudent.IUpdateClassStudentCommandHandler, EHub.Application.Features.Classes.UpdateClassStudent.UpdateClassStudentCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.RemoveStudentFromClass.IRemoveStudentFromClassCommandHandler, EHub.Application.Features.Classes.RemoveStudentFromClass.RemoveStudentFromClassCommandHandler>();

        services.AddScoped<EHub.Application.Common.Interfaces.Authorization.IPermissionService, EHub.Application.Common.Services.Authorization.PermissionService>();

        return services;
    }
}

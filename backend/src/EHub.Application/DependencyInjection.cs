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
using EHub.Application.Features.Dashboard.GetAdminDashboard;
using EHub.Application.Features.Tracking;
using EHub.Application.Features.Admin.Users.ManageUsers;
using EHub.Application.Features.Subjects.ManageSubjects;
using EHub.Application.Features.Subjects.ManageSemester;
using EHub.Application.Features.Subjects.TeachingStaff;
using EHub.Application.Features.Subjects.Curriculum;
using EHub.Application.Features.Subjects.Roadmap;
using EHub.Application.Features.Subjects.Rubrics;

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
        services.AddScoped<IGetAdminDashboardQueryHandler, GetAdminDashboardQueryHandler>();
        services.AddScoped<ITrackingQueryHandler, TrackingQueryHandler>();
        services.AddScoped<IUserManagementHandler, UserManagementHandler>();
        services.AddScoped<ISubjectManagementHandler, SubjectManagementHandler>();
        services.AddScoped<ICurrentSemesterHandler, CurrentSemesterHandler>();
        services.AddScoped<ITeachingStaffQueryHandler, TeachingStaffQueryHandler>();
        services.AddScoped<IGetSubjectCurriculumQueryHandler, GetSubjectCurriculumQueryHandler>();
        services.AddScoped<ISynchronizeSubjectCheckpointsHandler, SynchronizeSubjectCheckpointsHandler>();
        services.AddScoped<ISubjectRoadmapHandler, SubjectRoadmapHandler>();
        services.AddScoped<ISubjectRubricHandler, SubjectRubricHandler>();

        services.AddScoped<EHub.Application.Common.Interfaces.Authorization.IPermissionService, EHub.Application.Common.Services.Authorization.PermissionService>();

        return services;
    }
}

using System.Reflection;
using EHub.Application.Features.Admin.Users.ApproveUser;
using EHub.Application.Features.Admin.Users.GetPendingApprovalUsers;
using EHub.Application.Features.Admin.Users.ManageUsers;
using EHub.Application.Features.Admin.Users.RejectUser;
using EHub.Application.Features.Auth.ForgotPassword;
using EHub.Application.Features.Auth.GetCurrentUser;
using EHub.Application.Features.Auth.GoogleLogin;
using EHub.Application.Features.Auth.Login;
using EHub.Application.Features.Auth.Logout;
using EHub.Application.Features.Auth.RefreshToken;
using EHub.Application.Features.Auth.Register;
using EHub.Application.Features.Auth.ResetPassword;
using EHub.Application.Features.Dashboard.GetAdminDashboard;
using EHub.Application.Features.Subjects.Curriculum;
using EHub.Application.Features.Subjects.ManageSemester;
using EHub.Application.Features.Subjects.ManageSubjects;
using EHub.Application.Features.Subjects.Roadmap;
using EHub.Application.Features.Subjects.Rubrics;
using EHub.Application.Features.Subjects.TeachingStaff;
using EHub.Application.Features.Tracking;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddScoped<IUserManagementHandler, UserManagementHandler>();

        services.AddScoped<IGetAdminDashboardQueryHandler, GetAdminDashboardQueryHandler>();
        services.AddScoped<ITrackingQueryHandler, TrackingQueryHandler>();

        services.AddScoped<ISubjectManagementHandler, SubjectManagementHandler>();
        services.AddScoped<ICurrentSemesterHandler, CurrentSemesterHandler>();
        services.AddScoped<ITeachingStaffQueryHandler, TeachingStaffQueryHandler>();
        services.AddScoped<IGetSubjectCurriculumQueryHandler, GetSubjectCurriculumQueryHandler>();
        services.AddScoped<ISynchronizeSubjectCheckpointsHandler, SynchronizeSubjectCheckpointsHandler>();
        services.AddScoped<ISubjectRoadmapHandler, SubjectRoadmapHandler>();
        services.AddScoped<ISubjectRubricHandler, SubjectRubricHandler>();

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
        services.AddScoped<EHub.Application.Features.Classes.ReEnrollStudent.IReEnrollStudentCommandHandler, EHub.Application.Features.Classes.ReEnrollStudent.ReEnrollStudentCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.ImportStudents.IPreviewImportStudentsCommandHandler, EHub.Application.Features.Classes.ImportStudents.PreviewImportStudentsCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.ImportStudents.ICommitImportStudentsCommandHandler, EHub.Application.Features.Classes.ImportStudents.CommitImportStudentsCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.ExportClassRoster.IExportClassRosterQueryHandler, EHub.Application.Features.Classes.ExportClassRoster.ExportClassRosterQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.GetImportTemplate.IGetImportTemplateQueryHandler, EHub.Application.Features.Classes.GetImportTemplate.GetImportTemplateQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.GetMajorVerificationTemplate.IGetMajorVerificationTemplateQueryHandler, EHub.Application.Features.Classes.GetMajorVerificationTemplate.GetMajorVerificationTemplateQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.VerifyClassMajors.IVerifyClassMajorsCommandHandler, EHub.Application.Features.Classes.VerifyClassMajors.VerifyClassMajorsCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.SetEnrollmentMajorLock.ISetEnrollmentMajorLockCommandHandler, EHub.Application.Features.Classes.SetEnrollmentMajorLock.SetEnrollmentMajorLockCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.ClassLifecycle.IClassLifecycleCommandHandler, EHub.Application.Features.Classes.ClassLifecycle.ClassLifecycleCommandHandler>();
        services.AddScoped<EHub.Application.Features.Classes.ClassAudit.IGetClassAuditQueryHandler, EHub.Application.Features.Classes.ClassAudit.GetClassAuditQueryHandler>();
        services.AddScoped<EHub.Application.Features.Classes.RepairChatMemberships.IRepairClassChatMembershipsCommandHandler, EHub.Application.Features.Classes.RepairChatMemberships.RepairClassChatMembershipsCommandHandler>();
        services.AddScoped<EHub.Application.Features.Teams.ManageTeams.ITeamManagementHandler, EHub.Application.Features.Teams.ManageTeams.TeamManagementHandler>();
        services.AddScoped<EHub.Application.Features.Teams.MentorAssignments.IMentorAssignmentHandler, EHub.Application.Features.Teams.MentorAssignments.MentorAssignmentHandler>();
        services.AddScoped<EHub.Application.Features.Teams.TeamProposals.ITeamProposalHandler, EHub.Application.Features.Teams.TeamProposals.TeamProposalHandler>();
        services.AddScoped<EHub.Application.Features.Teams.ProjectDirections.IProjectDirectionHandler, EHub.Application.Features.Teams.ProjectDirections.ProjectDirectionHandler>();
        services.AddScoped<EHub.Application.Features.Classes.StudentSelfService.IStudentClassSelfServiceHandler, EHub.Application.Features.Classes.StudentSelfService.StudentClassSelfServiceHandler>();

        services.AddScoped<EHub.Application.Common.Interfaces.Authorization.IPermissionService, EHub.Application.Common.Services.Authorization.PermissionService>();

        return services;
    }
}

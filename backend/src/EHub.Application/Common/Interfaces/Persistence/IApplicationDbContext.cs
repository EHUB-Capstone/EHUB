using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Domain.Entities;

namespace EHub.Application.Common.Interfaces.Persistence;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PendingRegistration> PendingRegistrations { get; }
    DbSet<Semester> Semesters { get; }
    DbSet<SemesterAuditLog> SemesterAuditLogs { get; }
    DbSet<SemesterStaffAssignment> SemesterStaffAssignments { get; }
    DbSet<Course> Courses { get; }
    DbSet<Class> Classes { get; }
    DbSet<ClassLecturer> ClassLecturers { get; }
    DbSet<ClassAuditLog> ClassAuditLogs { get; }
    DbSet<ClassImportSession> ClassImportSessions { get; }
    DbSet<LecturerImportSession> LecturerImportSessions { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<Student> Students { get; }
    DbSet<ClassStudent> ClassStudents { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<TeamProposal> TeamProposals { get; }
    DbSet<TeamProposalMember> TeamProposalMembers { get; }
    DbSet<TeamProposalHistory> TeamProposalHistory { get; }
    DbSet<ProjectDirection> ProjectDirections { get; }
    DbSet<ProjectDirectionReview> ProjectDirectionReviews { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectTag> ProjectTags { get; }
    DbSet<ProjectActivityLog> ProjectActivityLogs { get; }
    DbSet<Checkpoint> Checkpoints { get; }
    DbSet<Submission> Submissions { get; }
    DbSet<SubmissionFile> SubmissionFiles { get; }
    DbSet<SubmissionFeedback> SubmissionFeedbacks { get; }
    DbSet<Rubric> Rubrics { get; }
    DbSet<RubricCriterion> RubricCriteria { get; }
    DbSet<Evaluation> Evaluations { get; }
    DbSet<EvaluationDetail> EvaluationDetails { get; }
    DbSet<EvaluationHistory> EvaluationHistories { get; }
    DbSet<MentorProfile> MentorProfiles { get; }
    DbSet<MentorAssignment> MentorAssignments { get; }
    DbSet<MentoringSession> MentoringSessions { get; }
    DbSet<MentoringActionItem> MentoringActionItems { get; }
    DbSet<MentoringAttendance> MentoringAttendances { get; }
    DbSet<AcademicDataset> AcademicDatasets { get; }
    DbSet<DataBankColumn> DataBankColumns { get; }
    DbSet<DataBankImportBatch> DataBankImportBatches { get; }
    DbSet<DataBankSnapshot> DataBankSnapshots { get; }
    DbSet<DataBankFieldHistory> DataBankFieldHistories { get; }
    DbSet<DataBankExportTemplate> DataBankExportTemplates { get; }
    DbSet<DataBankAuditLog> DataBankAuditLogs { get; }
    DbSet<ProjectProposal> ProjectProposals { get; }
    DbSet<ProjectProposalVersion> ProjectProposalVersions { get; }
    DbSet<ProjectComment> ProjectComments { get; }
    DbSet<PitchDeck> PitchDecks { get; }
    DbSet<ProjectShortcut> Shortcuts { get; }
    DbSet<StartupLineage> StartupLineages { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<ChatGroup> ChatGroups { get; }
    DbSet<ChatGroupMember> ChatGroupMembers { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Workshop> Workshops { get; }
    DbSet<WorkshopAttendance> WorkshopAttendances { get; }
    DbSet<Milestone> Milestones { get; }
    DbSet<SprintTask> SprintTasks { get; }
    DbSet<WeeklyTask> WeeklyTasks { get; }
    DbSet<ProjectAnalysis> ProjectAnalyses { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    void ClearChanges();
}

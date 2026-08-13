using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EHub.Application.Common.Interfaces.Persistence;
using EHub.Domain.Common;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence;

public class AppDbContext : DbContext, IApplicationDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<SemesterAuditLog> SemesterAuditLogs => Set<SemesterAuditLog>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassLecturer> ClassLecturers => Set<ClassLecturer>();
    public DbSet<ClassAuditLog> ClassAuditLogs => Set<ClassAuditLog>();
    public DbSet<ClassImportSession> ClassImportSessions => Set<ClassImportSession>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<TeamProposal> TeamProposals => Set<TeamProposal>();
    public DbSet<TeamProposalMember> TeamProposalMembers => Set<TeamProposalMember>();
    public DbSet<TeamProposalHistory> TeamProposalHistory => Set<TeamProposalHistory>();
    public DbSet<ProjectDirection> ProjectDirections => Set<ProjectDirection>();
    public DbSet<ProjectDirectionReview> ProjectDirectionReviews => Set<ProjectDirectionReview>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectTag> ProjectTags => Set<ProjectTag>();
    public DbSet<Checkpoint> Checkpoints => Set<Checkpoint>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<SubmissionFeedback> SubmissionFeedbacks => Set<SubmissionFeedback>();
    public DbSet<Rubric> Rubrics => Set<Rubric>();
    public DbSet<RubricCriterion> RubricCriteria => Set<RubricCriterion>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<EvaluationDetail> EvaluationDetails => Set<EvaluationDetail>();
    public DbSet<EvaluationHistory> EvaluationHistories => Set<EvaluationHistory>();
    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MentorAssignment> MentorAssignments => Set<MentorAssignment>();
    public DbSet<MentoringSession> MentoringSessions => Set<MentoringSession>();
    public DbSet<MentoringActionItem> MentoringActionItems => Set<MentoringActionItem>();
    public DbSet<MentoringAttendance> MentoringAttendances => Set<MentoringAttendance>();
    public DbSet<AcademicDataset> AcademicDatasets => Set<AcademicDataset>();
    public DbSet<DataBankColumn> DataBankColumns => Set<DataBankColumn>();
    public DbSet<DataBankImportBatch> DataBankImportBatches => Set<DataBankImportBatch>();
    public DbSet<DataBankSnapshot> DataBankSnapshots => Set<DataBankSnapshot>();
    public DbSet<DataBankFieldHistory> DataBankFieldHistories => Set<DataBankFieldHistory>();
    public DbSet<DataBankExportTemplate> DataBankExportTemplates => Set<DataBankExportTemplate>();
    public DbSet<DataBankAuditLog> DataBankAuditLogs => Set<DataBankAuditLog>();
    public DbSet<ProjectProposal> ProjectProposals => Set<ProjectProposal>();
    public DbSet<ProjectProposalVersion> ProjectProposalVersions => Set<ProjectProposalVersion>();
    public DbSet<ProjectComment> ProjectComments => Set<ProjectComment>();
    public DbSet<PitchDeck> PitchDecks => Set<PitchDeck>();
    public DbSet<ProjectShortcut> Shortcuts => Set<ProjectShortcut>();
    public DbSet<StartupLineage> StartupLineages => Set<StartupLineage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();
    public DbSet<ChatGroupMember> ChatGroupMembers => Set<ChatGroupMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Workshop> Workshops => Set<Workshop>();
    public DbSet<WorkshopAttendance> WorkshopAttendances => Set<WorkshopAttendance>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<SprintTask> SprintTasks => Set<SprintTask>();
    public DbSet<WeeklyTask> WeeklyTasks => Set<WeeklyTask>();
    public DbSet<ProjectAnalysis> ProjectAnalyses => Set<ProjectAnalysis>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;

                case EntityState.Deleted:
                    // Chuyển đổi Hard Delete thành Soft Delete
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    public void ClearChanges() => ChangeTracker.Clear();
}

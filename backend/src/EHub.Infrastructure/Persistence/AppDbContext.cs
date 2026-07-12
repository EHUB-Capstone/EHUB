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
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassLecturer> ClassLecturers => Set<ClassLecturer>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<ClassStudent> ClassStudents => Set<ClassStudent>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
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
}

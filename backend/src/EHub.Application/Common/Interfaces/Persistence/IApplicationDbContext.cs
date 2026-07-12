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
    DbSet<Semester> Semesters { get; }
    DbSet<Course> Courses { get; }
    DbSet<Class> Classes { get; }
    DbSet<ClassLecturer> ClassLecturers { get; }
    DbSet<Student> Students { get; }
    DbSet<ClassStudent> ClassStudents { get; }
    DbSet<Team> Teams { get; }
    DbSet<TeamMember> TeamMembers { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectTag> ProjectTags { get; }
    DbSet<Checkpoint> Checkpoints { get; }
    DbSet<Submission> Submissions { get; }
    DbSet<SubmissionFile> SubmissionFiles { get; }
    DbSet<SubmissionFeedback> SubmissionFeedbacks { get; }
    DbSet<Rubric> Rubrics { get; }
    DbSet<RubricCriterion> RubricCriteria { get; }
    DbSet<Evaluation> Evaluations { get; }
    DbSet<EvaluationDetail> EvaluationDetails { get; }
    DbSet<EvaluationHistory> EvaluationHistories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

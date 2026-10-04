using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class TeamContinuationConfiguration : IEntityTypeConfiguration<TeamContinuation>
{
    public void Configure(EntityTypeBuilder<TeamContinuation> builder)
    {
        builder.ToTable("team_continuations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.TeamLineageId).HasColumnName("team_lineage_id").IsRequired();
        builder.Property(c => c.SourceTeamId).HasColumnName("source_team_id");
        builder.Property(c => c.TargetSemesterId).HasColumnName("target_semester_id").IsRequired();
        builder.Property(c => c.TargetClassId).HasColumnName("target_class_id").IsRequired();
        builder.Property(c => c.CreatedTeamId).HasColumnName("created_team_id");

        builder.Property(c => c.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(c => c.DissolvedAtUtc).HasColumnName("dissolved_at_utc");
        builder.Property(c => c.DissolvedByUserId).HasColumnName("dissolved_by_user_id");

        builder.Property(c => c.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        // A lineage continues at most once per semester, however many times or in
        // however many batches the roster is imported.
        builder.HasIndex(c => new { c.TeamLineageId, c.TargetSemesterId }).IsUnique();
        builder.HasIndex(c => c.CreatedTeamId);
        builder.HasIndex(c => c.TargetClassId);

        builder.HasOne(c => c.SourceTeam)
            .WithMany()
            .HasForeignKey(c => c.SourceTeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.CreatedTeam)
            .WithMany()
            .HasForeignKey(c => c.CreatedTeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.TargetSemester)
            .WithMany()
            .HasForeignKey(c => c.TargetSemesterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.TargetClass)
            .WithMany()
            .HasForeignKey(c => c.TargetClassId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TeamContinuationMemberConfiguration : IEntityTypeConfiguration<TeamContinuationMember>
{
    public void Configure(EntityTypeBuilder<TeamContinuationMember> builder)
    {
        builder.ToTable("team_continuation_members");

        builder.HasKey(m => new { m.ContinuationId, m.StudentId });

        builder.Property(m => m.ContinuationId).HasColumnName("continuation_id");
        builder.Property(m => m.StudentId).HasColumnName("student_id");
        builder.Property(m => m.AddedAtUtc).HasColumnName("added_at_utc").IsRequired();

        builder.HasOne(m => m.Continuation)
            .WithMany(c => c.Members)
            .HasForeignKey(m => m.ContinuationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.Student)
            .WithMany()
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

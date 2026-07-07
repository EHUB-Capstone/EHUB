using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("teams");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.ClassId)
            .HasColumnName("class_id")
            .IsRequired();

        builder.Property(t => t.TeamCode)
            .HasColumnName("team_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.TeamName)
            .HasColumnName("team_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.LeaderId)
            .HasColumnName("leader_id");

        builder.Property(t => t.MentorId)
            .HasColumnName("mentor_id");

        builder.Property(t => t.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(t => t.ArchivedAt)
            .HasColumnName("archived_at");

        // Indexes & Constraints
        builder.HasIndex(t => new { t.ClassId, t.TeamCode })
            .IsUnique();

        builder.HasIndex(t => new { t.ClassId, t.TeamName })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Relationships configuration
        builder.HasOne(t => t.Class)
            .WithMany(c => c.Teams)
            .HasForeignKey(t => t.ClassId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Creator)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(t => t.MentorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(t => t.LeaderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

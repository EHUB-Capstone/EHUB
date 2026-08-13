using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SemesterConfiguration : IEntityTypeConfiguration<Semester>
{
    public void Configure(EntityTypeBuilder<Semester> builder)
    {
        builder.ToTable("semesters");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");

        builder.Property(s => s.Code)
            .HasColumnName("code")
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(s => s.Code)
            .IsUnique();

        builder.HasIndex(s => new { s.Term, s.Year })
            .IsUnique();

        builder.Property(s => s.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(s => s.Term)
            .HasColumnName("term")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(s => s.StartDate)
            .HasColumnName("start_date");

        builder.Property(s => s.EndDate)
            .HasColumnName("end_date");

        builder.Property(s => s.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(s => s.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.Property(s => s.CompletedByUserId)
            .HasColumnName("completed_by_user_id");

        builder.Property(s => s.CompletionReason)
            .HasColumnName("completion_reason")
            .HasMaxLength(500);

        builder.Property(s => s.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasIndex(s => s.Status)
            .IsUnique()
            .HasFilter("status = 'Active' AND is_deleted = false");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_semesters_date_range",
                "start_date IS NULL OR end_date IS NULL OR start_date <= end_date");
            table.HasCheckConstraint(
                "CK_semesters_completion_metadata",
                "status <> 'Completed' OR (completed_at_utc IS NOT NULL AND completion_reason IS NOT NULL)");
        });

        // Audit & Soft Delete properties configuration
        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(s => !s.IsDeleted);

        builder.HasOne(s => s.CompletedByUser)
            .WithMany()
            .HasForeignKey(s => s.CompletedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

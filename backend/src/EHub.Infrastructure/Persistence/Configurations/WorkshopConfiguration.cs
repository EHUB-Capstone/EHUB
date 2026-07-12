using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class WorkshopConfiguration : IEntityTypeConfiguration<Workshop>
{
    public void Configure(EntityTypeBuilder<Workshop> builder)
    {
        builder.ToTable("workshops");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");

        builder.Property(w => w.Title)
            .HasColumnName("title")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(w => w.Description)
            .HasColumnName("description");

        builder.Property(w => w.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.TargetAudience)
            .HasColumnName("target_audience")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(w => w.Format)
            .HasColumnName("format")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(w => w.BannerUrl)
            .HasColumnName("banner_url")
            .HasMaxLength(1000);

        builder.Property(w => w.AttachmentsJson)
            .HasColumnName("attachments_json")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(w => w.StartAt)
            .HasColumnName("start_at")
            .IsRequired();

        builder.Property(w => w.EndAt)
            .HasColumnName("end_at")
            .IsRequired();

        builder.Property(w => w.Location)
            .HasColumnName("location")
            .HasMaxLength(300);

        builder.Property(w => w.MeetingLink)
            .HasColumnName("meeting_link")
            .HasMaxLength(1000);

        builder.Property(w => w.CreatedById)
            .HasColumnName("created_by_id")
            .IsRequired();

        builder.Property(w => w.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        // Indexes
        builder.HasIndex(w => w.CreatedById);
        builder.HasIndex(w => w.Type);
        builder.HasIndex(w => w.TargetAudience);
        builder.HasIndex(w => w.Format);
        builder.HasIndex(w => w.Status);
        builder.HasIndex(w => w.StartAt);
        builder.HasIndex(w => new { w.StartAt, w.EndAt });
        builder.HasIndex(w => new { w.Status, w.StartAt });

        // Constraints
        builder.ToTable(t => t.HasCheckConstraint("CK_Workshop_EndAtAfterStartAt", "end_at > start_at"));

        // Audit & Soft Delete properties configuration
        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");
        builder.Property(w => w.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(w => !w.IsDeleted);

        // Relationships configuration
        builder.HasOne(w => w.Creator)
            .WithMany()
            .HasForeignKey(w => w.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

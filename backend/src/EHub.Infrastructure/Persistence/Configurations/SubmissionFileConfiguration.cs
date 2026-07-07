using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class SubmissionFileConfiguration : IEntityTypeConfiguration<SubmissionFile>
{
    public void Configure(EntityTypeBuilder<SubmissionFile> builder)
    {
        builder.ToTable("submission_files");

        builder.HasKey(sf => sf.Id);
        builder.Property(sf => sf.Id).HasColumnName("id");

        builder.Property(sf => sf.SubmissionId)
            .HasColumnName("submission_id")
            .IsRequired();

        builder.Property(sf => sf.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(sf => sf.OriginalName)
            .HasColumnName("original_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(sf => sf.FileUrl)
            .HasColumnName("file_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(sf => sf.CloudinaryPublicId)
            .HasColumnName("cloudinary_public_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(sf => sf.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(sf => sf.FileSize)
            .HasColumnName("file_size")
            .IsRequired();

        builder.Property(sf => sf.FileType)
            .HasColumnName("file_type")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(sf => sf.UploadedById)
            .HasColumnName("uploaded_by_id");

        builder.Property(sf => sf.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        // Audit & Soft Delete properties configuration
        builder.Property(sf => sf.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(sf => sf.CreatedBy).HasColumnName("created_by");
        builder.Property(sf => sf.UpdatedAt).HasColumnName("updated_at");
        builder.Property(sf => sf.UpdatedBy).HasColumnName("updated_by");
        builder.Property(sf => sf.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(sf => sf.DeletedAt).HasColumnName("deleted_at");
        builder.Property(sf => sf.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(sf => !sf.IsDeleted);

        // Relationships configuration
        builder.HasOne(sf => sf.Submission)
            .WithMany(s => s.Files)
            .HasForeignKey(sf => sf.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(sf => sf.UploadedBy)
            .WithMany()
            .HasForeignKey(sf => sf.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class PitchDeckConfiguration : IEntityTypeConfiguration<PitchDeck>
{
    public void Configure(EntityTypeBuilder<PitchDeck> builder)
    {
        builder.ToTable("pitch_decks");

        builder.HasKey(pd => pd.Id);
        builder.Property(pd => pd.Id).HasColumnName("id");

        builder.Property(pd => pd.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(pd => pd.ProjectProposalId)
            .HasColumnName("project_proposal_id");

        builder.Property(pd => pd.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(pd => pd.OriginalName)
            .HasColumnName("original_name")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(pd => pd.FileUrl)
            .HasColumnName("file_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(pd => pd.CloudinaryPublicId)
            .HasColumnName("cloudinary_public_id")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(pd => pd.MimeType)
            .HasColumnName("mime_type")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(pd => pd.FileSize)
            .HasColumnName("file_size")
            .IsRequired();

        builder.Property(pd => pd.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        builder.Property(pd => pd.UploadedById)
            .HasColumnName("uploaded_by_id")
            .IsRequired();

        builder.Property(pd => pd.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(pd => pd.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();

        // Indexes
        builder.HasIndex(pd => pd.ProjectId);
        builder.HasIndex(pd => pd.ProjectProposalId);
        builder.HasIndex(pd => pd.UploadedById);
        builder.HasIndex(pd => pd.Status);
        builder.HasIndex(pd => pd.UploadedAt);
        builder.HasIndex(pd => pd.CloudinaryPublicId)
            .IsUnique();

        builder.HasIndex(pd => new { pd.ProjectId, pd.VersionNumber })
            .IsUnique();

        // Audit & Soft Delete properties configuration
        builder.Property(pd => pd.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pd => pd.CreatedBy).HasColumnName("created_by");
        builder.Property(pd => pd.UpdatedAt).HasColumnName("updated_at");
        builder.Property(pd => pd.UpdatedBy).HasColumnName("updated_by");
        builder.Property(pd => pd.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(pd => pd.DeletedAt).HasColumnName("deleted_at");
        builder.Property(pd => pd.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(pd => !pd.IsDeleted);

        // Relationships configuration
        builder.HasOne(pd => pd.Project)
            .WithMany(p => p.PitchDecks)
            .HasForeignKey(pd => pd.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pd => pd.ProjectProposal)
            .WithMany(pp => pp.PitchDecks)
            .HasForeignKey(pd => pd.ProjectProposalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pd => pd.UploadedBy)
            .WithMany()
            .HasForeignKey(pd => pd.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

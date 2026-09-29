using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class SubmissionLinkConfiguration : IEntityTypeConfiguration<SubmissionLink>
{
    public void Configure(EntityTypeBuilder<SubmissionLink> builder)
    {
        builder.ToTable("submission_links");

        builder.HasKey(link => link.Id);
        builder.Property(link => link.Id).HasColumnName("id");
        builder.Property(link => link.SubmissionId).HasColumnName("submission_id").IsRequired();
        builder.Property(link => link.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(link => link.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
        builder.Property(link => link.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(link => link.SubmittedById).HasColumnName("submitted_by_id").IsRequired();
        builder.Property(link => link.SubmittedAt).HasColumnName("submitted_at").IsRequired();

        builder.HasIndex(link => new { link.SubmissionId, link.VersionNumber }).IsUnique();
        builder.HasIndex(link => link.SubmittedById);

        builder.Property(link => link.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(link => link.CreatedBy).HasColumnName("created_by");
        builder.Property(link => link.UpdatedAt).HasColumnName("updated_at");
        builder.Property(link => link.UpdatedBy).HasColumnName("updated_by");
        builder.Property(link => link.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(link => link.DeletedAt).HasColumnName("deleted_at");
        builder.Property(link => link.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(link => !link.IsDeleted);

        builder.HasOne(link => link.Submission)
            .WithMany(submission => submission.Links)
            .HasForeignKey(link => link.SubmissionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(link => link.SubmittedBy)
            .WithMany()
            .HasForeignKey(link => link.SubmittedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

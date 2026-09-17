using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class SubmissionRequirementContentConfiguration : IEntityTypeConfiguration<SubmissionRequirementContent>
{
    public void Configure(EntityTypeBuilder<SubmissionRequirementContent> builder)
    {
        builder.ToTable("submission_requirement_contents");
        builder.HasKey(content => content.Id);
        builder.Property(content => content.Id).HasColumnName("id");
        builder.Property(content => content.SubmissionId).HasColumnName("submission_id").IsRequired();
        builder.Property(content => content.RequirementIndex).HasColumnName("requirement_index").IsRequired();
        builder.Property(content => content.Content).HasColumnName("content").HasMaxLength(5_000).IsRequired();
        builder.HasIndex(content => new { content.SubmissionId, content.RequirementIndex }).IsUnique();

        builder.Property(content => content.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(content => content.CreatedBy).HasColumnName("created_by");
        builder.Property(content => content.UpdatedAt).HasColumnName("updated_at");
        builder.Property(content => content.UpdatedBy).HasColumnName("updated_by");
        builder.Property(content => content.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(content => content.DeletedAt).HasColumnName("deleted_at");
        builder.Property(content => content.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(content => !content.IsDeleted);
    }
}

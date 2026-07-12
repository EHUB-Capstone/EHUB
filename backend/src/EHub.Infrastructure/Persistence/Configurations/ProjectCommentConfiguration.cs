using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ProjectCommentConfiguration : IEntityTypeConfiguration<ProjectComment>
{
    public void Configure(EntityTypeBuilder<ProjectComment> builder)
    {
        builder.ToTable("project_comments");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.ProjectProposalId)
            .HasColumnName("project_proposal_id")
            .IsRequired();

        builder.Property(c => c.SectionKey)
            .HasColumnName("section_key")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.SectionLabel)
            .HasColumnName("section_label")
            .HasMaxLength(200);

        builder.Property(c => c.SelectedText)
            .HasColumnName("selected_text");

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(c => c.CreatedById)
            .HasColumnName("created_by_id");

        builder.Property(c => c.ParentCommentId)
            .HasColumnName("parent_comment_id");

        builder.Property(c => c.ThreadRootId)
            .HasColumnName("thread_root_id");

        builder.Property(c => c.Resolved)
            .HasColumnName("resolved")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.ResolvedById)
            .HasColumnName("resolved_by_id");

        builder.Property(c => c.ResolvedAt)
            .HasColumnName("resolved_at");

        // Indexes
        builder.HasIndex(c => c.ProjectProposalId);
        builder.HasIndex(c => c.SectionKey);
        builder.HasIndex(c => c.CreatedById);
        builder.HasIndex(c => c.ParentCommentId);
        builder.HasIndex(c => c.ThreadRootId);
        builder.HasIndex(c => c.Resolved);
        builder.HasIndex(c => new { c.ProjectProposalId, c.SectionKey });
        builder.HasIndex(c => new { c.ProjectProposalId, c.Resolved });

        // Audit & Soft Delete properties configuration
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Relationships configuration
        builder.HasOne(c => c.ProjectProposal)
            .WithMany(pp => pp.Comments)
            .HasForeignKey(c => c.ProjectProposalId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Creator)
            .WithMany()
            .HasForeignKey(c => c.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ParentComment)
            .WithMany(pc => pc.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ThreadRoot)
            .WithMany(tr => tr.ThreadReplies)
            .HasForeignKey(c => c.ThreadRootId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ResolvedBy)
            .WithMany()
            .HasForeignKey(c => c.ResolvedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

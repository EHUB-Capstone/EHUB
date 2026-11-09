using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseCommentConfiguration : IEntityTypeConfiguration<ShowcaseComment>
{
    public void Configure(EntityTypeBuilder<ShowcaseComment> builder)
    {
        builder.ToTable("showcase_comments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ShowcaseProjectId).HasColumnName("showcase_project_id").IsRequired();
        builder.Property(item => item.AuthorUserId).HasColumnName("author_user_id").IsRequired();
        builder.Property(item => item.ParentCommentId).HasColumnName("parent_comment_id");
        builder.Property(item => item.Content).HasColumnName("content").HasMaxLength(2_000).IsRequired();
        builder.Property(item => item.IsTeamReply).HasColumnName("is_team_reply").HasDefaultValue(false);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasIndex(item => new { item.ShowcaseProjectId, item.CreatedAt });
        builder.HasIndex(item => item.ParentCommentId);
        builder.HasIndex(item => item.AuthorUserId);
        builder.HasQueryFilter(item => !item.IsDeleted);
        builder.HasOne(item => item.ShowcaseProject).WithMany().HasForeignKey(item => item.ShowcaseProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.AuthorUser).WithMany().HasForeignKey(item => item.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.ParentComment).WithMany(item => item.Replies).HasForeignKey(item => item.ParentCommentId).OnDelete(DeleteBehavior.Restrict);
    }
}

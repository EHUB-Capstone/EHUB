using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class MentoringFeedbackConfiguration : IEntityTypeConfiguration<MentoringFeedback>
{
    public void Configure(EntityTypeBuilder<MentoringFeedback> builder)
    {
        builder.ToTable("mentoring_feedback");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MentoringSessionId).HasColumnName("mentoring_session_id").IsRequired();
        builder.Property(x => x.StudentUserId).HasColumnName("student_user_id").IsRequired();
        builder.Property(x => x.Rating).HasColumnName("rating").IsRequired();
        builder.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(x => x.DeletedAt).HasColumnName("deleted_at");
        builder.Property(x => x.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasOne(x => x.MentoringSession).WithMany(x => x.Feedback).HasForeignKey(x => x.MentoringSessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StudentUser).WithMany().HasForeignKey(x => x.StudentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MentoringSessionId, x.StudentUserId }).IsUnique().HasFilter("is_deleted = false");
        builder.ToTable(x => x.HasCheckConstraint("CK_MentoringFeedback_Rating", "rating >= 1 AND rating <= 5"));
    }
}

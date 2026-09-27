using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class EvaluationMemberScoreConfiguration : IEntityTypeConfiguration<EvaluationMemberScore>
{
    public void Configure(EntityTypeBuilder<EvaluationMemberScore> builder)
    {
        builder.ToTable("evaluation_member_scores");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.EvaluationId).HasColumnName("evaluation_id").IsRequired();
        builder.Property(item => item.StudentId).HasColumnName("student_id").IsRequired();
        builder.Property(item => item.Score).HasColumnName("score").HasColumnType("decimal(6,2)").IsRequired();

        builder.HasIndex(item => new { item.EvaluationId, item.StudentId })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasOne(item => item.Evaluation)
            .WithMany(evaluation => evaluation.MemberScores)
            .HasForeignKey(item => item.EvaluationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(item => item.Student)
            .WithMany()
            .HasForeignKey(item => item.StudentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

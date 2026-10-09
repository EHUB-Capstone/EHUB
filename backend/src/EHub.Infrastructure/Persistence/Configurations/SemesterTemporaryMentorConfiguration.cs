using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class SemesterTemporaryMentorConfiguration : IEntityTypeConfiguration<SemesterTemporaryMentor>
{
    public void Configure(EntityTypeBuilder<SemesterTemporaryMentor> builder)
    {
        builder.ToTable("semester_temporary_mentors");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SemesterId).HasColumnName("semester_id").IsRequired();
        builder.Property(item => item.DraftId).HasColumnName("draft_id").IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.Version).IsRowVersion().HasColumnName("xmin");

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasIndex(item => new { item.SemesterId, item.DraftId }).IsUnique();
        builder.HasIndex(item => new { item.SemesterId, item.Status });
        builder.HasOne(item => item.Semester).WithMany().HasForeignKey(item => item.SemesterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Draft).WithMany().HasForeignKey(item => item.DraftId).OnDelete(DeleteBehavior.Restrict);
    }
}

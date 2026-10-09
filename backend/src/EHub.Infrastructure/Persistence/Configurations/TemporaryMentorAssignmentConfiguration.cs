using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class TemporaryMentorAssignmentConfiguration : IEntityTypeConfiguration<TemporaryMentorAssignment>
{
    public void Configure(EntityTypeBuilder<TemporaryMentorAssignment> builder)
    {
        builder.ToTable("temporary_mentor_assignments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TeamId).HasColumnName("team_id").IsRequired();
        builder.Property(item => item.DraftId).HasColumnName("draft_id").IsRequired();
        builder.Property(item => item.AssignedById).HasColumnName("assigned_by_id").IsRequired();
        builder.Property(item => item.AssignedAt).HasColumnName("assigned_at").IsRequired();
        builder.Property(item => item.EndedAt).HasColumnName("ended_at");
        builder.Property(item => item.Slot).HasColumnName("slot").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.Note).HasColumnName("note").HasMaxLength(1000);

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");
        builder.HasQueryFilter(item => !item.IsDeleted);

        builder.HasIndex(item => item.DraftId);
        builder.HasIndex(item => new { item.TeamId, item.Slot, item.Status });
        // At most one active temporary mentor per team slot; the handlers also keep a real mentor out of that slot.
        builder.HasIndex(item => new { item.TeamId, item.Slot })
            .IsUnique()
            .HasFilter("status = 'Active' AND is_deleted = false");

        builder.HasOne(item => item.Team).WithMany(team => team.TemporaryMentorAssignments)
            .HasForeignKey(item => item.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Draft).WithMany()
            .HasForeignKey(item => item.DraftId).OnDelete(DeleteBehavior.Restrict);
    }
}

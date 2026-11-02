using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class ShowcaseReviewHistoryConfiguration : IEntityTypeConfiguration<ShowcaseReviewHistory>
{
    public void Configure(EntityTypeBuilder<ShowcaseReviewHistory> builder)
    {
        builder.ToTable("showcase_review_histories");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id");

        builder.Property(h => h.ShowcaseProjectId).HasColumnName("showcase_project_id").IsRequired();
        builder.Property(h => h.FromStatus)
            .HasColumnName("from_status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(h => h.ToStatus)
            .HasColumnName("to_status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();
        builder.Property(h => h.Note).HasColumnName("note").HasMaxLength(1000);
        builder.Property(h => h.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(h => h.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(h => new { h.ShowcaseProjectId, h.CreatedAt });

        builder.HasOne(h => h.ShowcaseProject)
            .WithMany(s => s.ReviewHistory)
            .HasForeignKey(h => h.ShowcaseProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Actor)
            .WithMany()
            .HasForeignKey(h => h.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

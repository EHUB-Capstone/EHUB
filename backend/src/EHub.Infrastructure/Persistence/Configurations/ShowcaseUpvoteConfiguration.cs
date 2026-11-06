using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseUpvoteConfiguration : IEntityTypeConfiguration<ShowcaseUpvote>
{
    public void Configure(EntityTypeBuilder<ShowcaseUpvote> builder)
    {
        builder.ToTable("showcase_upvotes");

        builder.HasKey(vote => new { vote.ShowcaseProjectId, vote.UserId });
        builder.Property(vote => vote.ShowcaseProjectId).HasColumnName("showcase_project_id");
        builder.Property(vote => vote.UserId).HasColumnName("user_id");
        builder.Property(vote => vote.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(vote => vote.UserId);

        builder.HasOne<ShowcaseProject>()
            .WithMany()
            .HasForeignKey(vote => vote.ShowcaseProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

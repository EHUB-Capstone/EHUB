using EHub.Domain.Entities;
using EHub.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class MentorEmbeddingConfiguration : IEntityTypeConfiguration<MentorEmbedding>
{
    public void Configure(EntityTypeBuilder<MentorEmbedding> builder)
    {
        builder.ToTable("mentor_embeddings");
        builder.HasKey(x => x.MentorProfileId);
        builder.Property(x => x.MentorProfileId).HasColumnName("mentor_profile_id");
        builder.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ModelName).HasColumnName("model_name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("vector(1024)").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne<MentorProfile>().WithOne().HasForeignKey<MentorEmbedding>(x => x.MentorProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ProjectProposalEmbeddingConfiguration : IEntityTypeConfiguration<ProjectProposalEmbedding>
{
    public void Configure(EntityTypeBuilder<ProjectProposalEmbedding> builder)
    {
        builder.ToTable("project_proposal_embeddings", table =>
            table.HasCheckConstraint("CK_ProjectProposalEmbedding_Dimension", "dimension > 0 AND dimension <= 3072"));

        builder.HasKey(embedding => embedding.Id);
        builder.Property(embedding => embedding.Id).HasColumnName("id");
        builder.Property(embedding => embedding.ProposalVersionId).HasColumnName("proposal_version_id").IsRequired();
        builder.Property(embedding => embedding.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        builder.Property(embedding => embedding.TextSchemaVersion).HasColumnName("text_schema_version").HasMaxLength(100).IsRequired();
        builder.Property(embedding => embedding.Provider).HasColumnName("provider").HasMaxLength(100).IsRequired();
        builder.Property(embedding => embedding.Model).HasColumnName("model").HasMaxLength(100).IsRequired();
        builder.Property(embedding => embedding.Dimension).HasColumnName("dimension").IsRequired();
        builder.Property(embedding => embedding.VectorJson).HasColumnName("vector_json").HasColumnType("jsonb").IsRequired();
        builder.Property(embedding => embedding.GeneratedAtUtc).HasColumnName("generated_at_utc").IsRequired();

        builder.HasIndex(embedding => embedding.ProposalVersionId).IsUnique();
        builder.HasIndex(embedding => new
        {
            embedding.ContentHash,
            embedding.TextSchemaVersion,
            embedding.Provider,
            embedding.Model,
            embedding.Dimension
        });

        builder.HasOne(embedding => embedding.ProposalVersion)
            .WithOne(version => version.Embedding)
            .HasForeignKey<ProjectProposalEmbedding>(embedding => embedding.ProposalVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

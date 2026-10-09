using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class MentorExperienceConfiguration : IEntityTypeConfiguration<MentorExperience>
{
    public void Configure(EntityTypeBuilder<MentorExperience> builder)
    {
        builder.ToTable("mentor_experiences", table =>
        {
            table.HasCheckConstraint("ck_mentor_experience_kind", "kind IN ('Startup', 'Technology')");
            table.HasCheckConstraint("ck_mentor_experience_years", "years IS NULL OR (years >= 0 AND years <= 80)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.MentorProfileId).HasColumnName("mentor_profile_id");
        builder.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(20);
        builder.Property(x => x.Area).HasColumnName("area").HasMaxLength(80);
        builder.Property(x => x.Years).HasColumnName("years").HasPrecision(4, 1);
        builder.Property(x => x.Level).HasColumnName("level").HasMaxLength(40);
        builder.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.HasIndex(x => new { x.MentorProfileId, x.Kind, x.Area }).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using EHub.Domain.Entities;

namespace EHub.Infrastructure.Persistence.Configurations;

public class MentorProfileConfiguration : IEntityTypeConfiguration<MentorProfile>
{
    public void Configure(EntityTypeBuilder<MentorProfile> builder)
    {
        builder.ToTable("mentor_profiles");

        builder.HasKey(mp => mp.Id);
        builder.Property(mp => mp.Id).HasColumnName("id");

        builder.Property(mp => mp.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(mp => mp.Expertise)
            .HasColumnName("expertise")
            .HasColumnType("text[]");

        builder.Property(mp => mp.Bio)
            .HasColumnName("bio")
            .HasMaxLength(2000);

        builder.Property(mp => mp.Organization)
            .HasColumnName("organization")
            .HasMaxLength(200);

        builder.Property(mp => mp.LinkedInUrl)
            .HasColumnName("linkedin_url")
            .HasMaxLength(500);

        builder.Property(mp => mp.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(mp => mp.MaxTeams)
            .HasColumnName("max_teams")
            .IsRequired();

        // Unique index: One User has one MentorProfile
        builder.HasIndex(mp => mp.UserId)
            .IsUnique();

        builder.HasIndex(mp => mp.Status);
        builder.HasIndex(mp => mp.Organization);

        // Check constraint
        builder.ToTable(t => t.HasCheckConstraint("CK_MentorProfile_MaxTeams", "max_teams >= 0"));

        // Audit & Soft Delete properties configuration
        builder.Property(mp => mp.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(mp => mp.CreatedBy).HasColumnName("created_by");
        builder.Property(mp => mp.UpdatedAt).HasColumnName("updated_at");
        builder.Property(mp => mp.UpdatedBy).HasColumnName("updated_by");
        builder.Property(mp => mp.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(mp => mp.DeletedAt).HasColumnName("deleted_at");
        builder.Property(mp => mp.DeletedBy).HasColumnName("deleted_by");

        // Global query filter for soft delete
        builder.HasQueryFilter(mp => !mp.IsDeleted);

        // Relationships configuration
        builder.HasOne(mp => mp.User)
            .WithOne(u => u.MentorProfile)
            .HasForeignKey<MentorProfile>(mp => mp.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

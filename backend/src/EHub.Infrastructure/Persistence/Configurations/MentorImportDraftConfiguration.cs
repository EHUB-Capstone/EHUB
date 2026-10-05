using EHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class MentorImportDraftConfiguration : IEntityTypeConfiguration<MentorImportDraft>
{
    public void Configure(EntityTypeBuilder<MentorImportDraft> builder)
    {
        builder.ToTable("mentor_import_drafts");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SemesterId).HasColumnName("semester_id").IsRequired();
        builder.Property(item => item.Type).HasColumnName("mentor_type").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.FullName).HasColumnName("full_name").HasMaxLength(100).IsRequired();
        builder.Property(item => item.NormalizedFullName).HasColumnName("normalized_full_name").HasMaxLength(100).IsRequired();
        builder.Property(item => item.SourceOrdinal).HasColumnName("source_ordinal").HasMaxLength(30);
        builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(320);
        builder.Property(item => item.FptEmail).HasColumnName("fpt_email").HasMaxLength(320);
        builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(30);
        builder.Property(item => item.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        builder.Property(item => item.ContractType).HasColumnName("contract_type").HasMaxLength(100);
        builder.Property(item => item.EducationLevel).HasColumnName("education_level").HasMaxLength(200);
        builder.Property(item => item.CurrentAddress).HasColumnName("current_address").HasMaxLength(500);
        builder.Property(item => item.Organization).HasColumnName("organization").HasMaxLength(200);
        builder.Property(item => item.Department).HasColumnName("department").HasMaxLength(200);
        builder.Property(item => item.JobTitle).HasColumnName("job_title").HasMaxLength(200);
        builder.Property(item => item.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(item => item.ConvertedMentorProfileId).HasColumnName("converted_mentor_profile_id");
        builder.Property(item => item.ConvertedAtUtc).HasColumnName("converted_at_utc");

        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.CreatedBy).HasColumnName("created_by");
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        builder.Property(item => item.UpdatedBy).HasColumnName("updated_by");
        builder.Property(item => item.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.Property(item => item.DeletedAt).HasColumnName("deleted_at");
        builder.Property(item => item.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(item => !item.IsDeleted);
        builder.HasIndex(item => new { item.SemesterId, item.Type, item.NormalizedFullName });
        builder.HasIndex(item => new { item.SemesterId, item.Status });
        builder.HasOne(item => item.Semester).WithMany().HasForeignKey(item => item.SemesterId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.ConvertedMentorProfile).WithMany().HasForeignKey(item => item.ConvertedMentorProfileId).OnDelete(DeleteBehavior.SetNull);
    }
}

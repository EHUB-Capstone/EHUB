using EHub.Domain.Entities;
using EHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EHub.Infrastructure.Persistence.Configurations;

public sealed class ShowcaseViolationCategoryDefinitionConfiguration : IEntityTypeConfiguration<ShowcaseViolationCategoryDefinition>
{
    public void Configure(EntityTypeBuilder<ShowcaseViolationCategoryDefinition> builder)
    {
        builder.ToTable("showcase_violation_categories");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code).HasColumnName("code").HasConversion<string>().HasMaxLength(32).ValueGeneratedNever();
        builder.Property(x => x.NameVi).HasColumnName("name_vi").HasMaxLength(120).IsRequired();
        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.HasData(
            new { Code = ShowcaseViolationCategory.Profanity, NameVi = "Tục tĩu", NameEn = "Profanity", SortOrder = 1 },
            new { Code = ShowcaseViolationCategory.Insult, NameVi = "Xúc phạm, công kích cá nhân", NameEn = "Insults and personal attacks", SortOrder = 2 },
            new { Code = ShowcaseViolationCategory.Threat, NameVi = "Đe dọa, kích động bạo lực", NameEn = "Threats and incitement to violence", SortOrder = 3 },
            new { Code = ShowcaseViolationCategory.Defamation, NameVi = "Cáo buộc công kích, bôi nhọ", NameEn = "Abusive accusations and defamation", SortOrder = 4 },
            new { Code = ShowcaseViolationCategory.Racism, NameVi = "Miệt thị chủng tộc", NameEn = "Racism and racial abuse", SortOrder = 5 },
            new { Code = ShowcaseViolationCategory.AdultContent, NameVi = "Nội dung người lớn", NameEn = "Explicit adult content", SortOrder = 6 },
            new { Code = ShowcaseViolationCategory.Spam, NameVi = "Spam, quảng cáo bị cấm", NameEn = "Spam and prohibited promotions", SortOrder = 7 });
    }
}

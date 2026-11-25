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
        builder.Property(x => x.DescriptionVi).HasColumnName("description_vi").HasMaxLength(600).IsRequired();
        builder.Property(x => x.DescriptionEn).HasColumnName("description_en").HasMaxLength(600).IsRequired();
        builder.Property(x => x.SupportsPhraseBlocking).HasColumnName("supports_phrase_blocking").ValueGeneratedNever();
        builder.Property(x => x.SortOrder).HasColumnName("sort_order");
        builder.HasData(
            new { Code = ShowcaseViolationCategory.Profanity, NameVi = "Tục tĩu", NameEn = "Profanity", SortOrder = 1, SupportsPhraseBlocking = true,
                DescriptionVi = "Từ hoặc cụm từ tục tĩu được Admin phê duyệt. Quy tắc đối chiếu theo ranh giới từ và giữ phân biệt dấu tiếng Việt.",
                DescriptionEn = "Administrator-approved profane words or phrases. Matching respects word boundaries and explicit Vietnamese accents." },
            new { Code = ShowcaseViolationCategory.Insult, NameVi = "Xúc phạm, công kích cá nhân", NameEn = "Insults and personal attacks", SortOrder = 2, SupportsPhraseBlocking = true,
                DescriptionVi = "Lăng mạ hoặc quấy rối cá nhân. Góp ý xây dựng và ý kiến phản biện bình thường vẫn được phép.",
                DescriptionEn = "Insults, harassment or personal attacks. Constructive criticism and ordinary disagreement remain allowed." },
            new { Code = ShowcaseViolationCategory.Threat, NameVi = "Đe dọa, kích động bạo lực", NameEn = "Threats and incitement to violence", SortOrder = 3, SupportsPhraseBlocking = true,
                DescriptionVi = "Cụm từ đe dọa trực tiếp hoặc cổ súy hành vi bạo lực. Người duyệt cần xem ngữ cảnh trước khi bổ sung quy tắc.",
                DescriptionEn = "Direct threats or calls for violence. Review the context before adding a phrase to the shared blocklist." },
            new { Code = ShowcaseViolationCategory.Defamation, NameVi = "Cáo buộc công kích, bôi nhọ", NameEn = "Abusive accusations and defamation", SortOrder = 4, SupportsPhraseBlocking = true,
                DescriptionVi = "Cáo buộc hoặc ngôn từ bôi nhọ mang tính công kích. Bộ lọc không tự xác minh sự thật hay kết luận vi phạm pháp luật.",
                DescriptionEn = "Abusive accusations or defamatory language. Keyword matching does not verify facts or determine legal violations." },
            new { Code = ShowcaseViolationCategory.Racism, NameVi = "Miệt thị chủng tộc", NameEn = "Racism and racial abuse", SortOrder = 5, SupportsPhraseBlocking = true,
                DescriptionVi = "Miệt thị hoặc công kích về chủng tộc, dân tộc. Không chặn các thảo luận học thuật trung lập chỉ vì nhắc đến một nhóm người.",
                DescriptionEn = "Racial or ethnic abuse. Neutral academic discussion must not be blocked simply for mentioning a group." },
            new { Code = ShowcaseViolationCategory.AdultContent, NameVi = "Nội dung người lớn", NameEn = "Explicit adult content", SortOrder = 6, SupportsPhraseBlocking = true,
                DescriptionVi = "Nội dung tình dục hoặc khiêu dâm rõ ràng. Các thảo luận giáo dục, y tế cần được đánh giá đúng ngữ cảnh.",
                DescriptionEn = "Explicit sexual or pornographic content. Educational and health discussions require contextual judgment." },
            new { Code = ShowcaseViolationCategory.Spam, NameVi = "Spam, quảng cáo bị cấm", NameEn = "Spam and prohibited promotions", SortOrder = 7, SupportsPhraseBlocking = true,
                DescriptionVi = "Bình luận lặp lại hoặc quảng cáo không phù hợp. Liên kết demo, tài liệu và góp ý hợp lệ không tự động bị cấm.",
                DescriptionEn = "Repeated comments or unwanted promotions. Legitimate demo links, documentation and feedback are not automatically prohibited." },
            new { Code = ShowcaseViolationCategory.ProhibitedContent, NameVi = "Nội dung cấm", NameEn = "Prohibited content", SortOrder = 8, SupportsPhraseBlocking = false,
                DescriptionVi = "Báo cáo nội dung nghi quảng bá hàng cấm, chất cấm, cá cược hoặc lừa đảo. Staff đánh giá bằng ngữ cảnh; không chặn bằng từ khóa của nhóm này.",
                DescriptionEn = "Report suspected prohibited goods, drugs, gambling or scams. Staff assess context; this category does not create keyword-blocking rules." },
            new { Code = ShowcaseViolationCategory.Misinformation, NameVi = "Thông tin sai lệch", NameEn = "Misinformation", SortOrder = 9, SupportsPhraseBlocking = false,
                DescriptionVi = "Báo cáo thông tin nghi sai lệch hoặc khẳng định gây hiểu nhầm. Người duyệt kiểm tra căn cứ; hệ thống không tự xác minh đúng sai.",
                DescriptionEn = "Report suspected false or misleading claims. Reviewers check the evidence; the system does not automatically fact-check content." },
            new { Code = ShowcaseViolationCategory.OffTopic, NameVi = "Lạc chủ đề", NameEn = "Off-topic", SortOrder = 10, SupportsPhraseBlocking = false,
                DescriptionVi = "Báo cáo bình luận không liên quan đến dự án hoặc gây gián đoạn trao đổi. Staff xem nội dung cuộc thảo luận trước khi quyết định.",
                DescriptionEn = "Report comments unrelated to the project or disruptive to discussion. Staff review the conversation before deciding." });
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EHub.Application.Features.Mentoring.TrainedRanking;
using EHub.Shared.Errors;
using EHub.Shared.Results;

namespace EHub.Application.Features.Mentoring.ExtractCv;

public sealed record MentorCvProfileData(string? Bio, string? Experience, string[] Expertise);

// Keep the original professional wording for semantic matching; do not infer employers, years or mentor type.
public static class MentorCvProfileParser
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly (string Label, string[] Aliases)[] ExpertiseKeywords =
    [
        ("Marketing", ["marketing", "tiep thi"]),
        ("Finance", ["finance", "tai chinh"]),
        ("Business administration", ["business administration", "quan tri kinh doanh"]),
        ("Business development", ["business development", "phat trien kinh doanh"]),
        ("Operations management", ["operations", "van hanh"]),
        ("Project management", ["project management", "quan ly du an", "dieu phoi du an"]),
        ("Strategy", ["strategy", "chien luoc"]),
        ("Product management", ["product management", "quan ly san pham"]),
        ("Sales", ["sales", "ban hang"]),
        ("AI", ["ai", "artificial intelligence", "tri tue nhan tao"]),
        ("Data science", ["data science", "khoa hoc du lieu"]),
        ("Software engineering", ["software engineering", "ky thuat phan mem"]),
        ("Cloud", ["cloud", "dien toan dam may"]),
        ("Cybersecurity", ["cybersecurity", "an toan thong tin"])
    ];
    public static Result<MentorCvProfileData> Parse(string text)
    {
        var summary = new List<string>();
        var experience = new List<string>();
        var declaredExpertise = new List<string>();
        Section section = Section.Excluded;
        foreach (var sourceLine in text.Replace('\r', '\n').Split('\n'))
        {
            var line = Regex.Replace(sourceLine, @"\s+", " ", RegexOptions.None, RegexTimeout).Trim();
            if (line.Length == 0) continue;
            var normalized = Normalize(line);
            if (IsPrivate(normalized) || IsSignature(normalized))
            {
                section = Section.Excluded;
                continue;
            }
            var heading = Heading(normalized);
            if (heading is not null)
            {
                section = heading.Value;
                // A heading may contain content after a colon, e.g. "Education: Demo University".
                var headingText = WithoutHeadingNumber(line);
                var colon = headingText.IndexOf(':');
                if (colon < 0 || colon == headingText.Length - 1) continue;
                line = headingText[(colon + 1)..].Trim();
            }
            else if (Regex.IsMatch(normalized, @"^[ivx]+[.)]\s", RegexOptions.None, RegexTimeout))
            {
                section = Section.Excluded;
                continue;
            }
            if (section == Section.Excluded) continue;
            // Strip contact details even if placed inside a professional section.
            line = Regex.Replace(line, @"[\w.+-]+@[\w.-]+\.[\p{L}]{2,}|https?://\S+|(?<!\d)(?:\+?\d[ ().-]*){9,15}(?!\d)",
                "", RegexOptions.IgnoreCase, RegexTimeout).Trim();
            if (line.Length == 0) continue;
            (section == Section.Experience ? experience : summary).Add(line);
            if (section != Section.Education) declaredExpertise.Add(line);
        }
        if (summary.Count == 0 && experience.Count == 0)
            return Result.Failure<MentorCvProfileData>(ErrorCodes.CommonValidationError,
                "Không tìm thấy mục học vấn, kinh nghiệm, kỹ năng chuyên môn hoặc giới thiệu trong CV. Vui lòng dùng tiêu đề rõ ràng cho các mục này.");

        // Preserve the original CV text, but infer tags only from affirmative work/skills.
        // A degree alone is retained in Bio and does not become declared expertise.
        var professionalText = MentorEvidenceText.Positive(string.Join('\n', declaredExpertise));
        var expertise = ExpertiseKeywords.Where(skill => skill.Aliases.Any(alias => ContainsTerm(professionalText, alias)))
            .Select(skill => skill.Label).ToArray();
        return Result.Success(new MentorCvProfileData(Limit(summary, 2000), Limit(experience, 4000), expertise));
    }

    private static string? Limit(List<string> lines, int length)
    {
        var value = string.Join('\n', lines);
        return value.Length == 0 ? null : value[..Math.Min(value.Length, length)];
    }

    private static bool ContainsTerm(string text, string term) => Regex.IsMatch(text,
        @"(?<![\p{L}\p{N}+#.])" + Regex.Escape(term) + @"(?![\p{L}\p{N}+#])", RegexOptions.None, RegexTimeout);

    private static Section? Heading(string value)
    {
        // "Vi" in the plain Vietnamese heading is a word, not the section number VI.
        if (Starts(value, "vi tinh")) return Section.Excluded;
        value = WithoutHeadingNumber(value);
        if (Starts(value, "computer skills", "computer literacy", "office skills", "tin hoc van phong", "vi tinh"))
            return Section.Excluded;
        if (Starts(value, "technical skills", "technology skills", "professional skills", "ky nang", "skills"))
            return Section.Summary;
        if (Starts(value, "kinh nghiem lam viec", "kinh nghiem cong tac", "qua trinh cong tac", "work experience", "professional experience", "employment history", "experience"))
            return Section.Experience;
        if (Starts(value, "qua trinh dao tao", "trinh do hoc van", "hoc van", "education", "academic background", "qualifications"))
            return Section.Education;
        if (Starts(value, "trinh do ngoai ngu", "ngoai ngu", "languages", "certifications", "chung chi", "professional summary", "summary", "gioi thieu", "cac thanh tich", "thanh tich", "giai thuong", "awards", "cac cong trinh nghien cuu", "research", "cac bai bao", "publications"))
            return Section.Summary;
        return null;
    }

    private static string WithoutHeadingNumber(string value) => Regex.Replace(value,
        @"^(?:\((?:[ivx]+|\d+(?:\.\d+)*)\)\s*|(?:[ivx]+|\d+(?:\.\d+)*)(?:[.)]\s*|\s*[-–—:]\s*|\s+))",
        "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout).Trim();

    private static bool Starts(string value, params string[] prefixes) => prefixes.Any(prefix =>
        value == prefix || value.StartsWith(prefix + " ", StringComparison.Ordinal) || value.StartsWith(prefix + ":", StringComparison.Ordinal));

    private static bool IsPrivate(string value) => Regex.IsMatch(value,
        @"\b(thong tin ca nhan|thong tin nguoi than|personal (information|details)|family|ho va ten|full name|ngay sinh|date of birth|noi sinh|gioi tinh|dan toc|cmnd|cccd|can cuoc|ho chieu|passport|dia chi|ho khau|hon nhan|suc khoe|ma so thue|tax id|bank account|bhxh|so thich|hobbies|so truong, so doan|references)\b",
        RegexOptions.None, RegexTimeout) || Regex.IsMatch(WithoutHeadingNumber(value),
        // Contact labels are private; "apps on a phone", "email marketing" and
        // professional banking experience are not contact fields.
        @"^(?:dien thoai|phone|email|address|ngan hang)(?:\s*[:：]|\s*$|\s+(?:\+?\d|[^\s]+@))",
        RegexOptions.None, RegexTimeout);

    private static bool IsSignature(string value) => Starts(value, "nguoi khai", "ky va ghi ro", "signature", "declaration") ||
        Regex.IsMatch(value, @"\bngay \d{1,2} thang \d{1,2} nam \d{4}\b", RegexOptions.None, RegexTimeout);

    private static string Normalize(string value)
    {
        var normalized = new StringBuilder();
        foreach (var character in value.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                normalized.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        return normalized.ToString().Normalize(NormalizationForm.FormC);
    }

    private enum Section { Excluded, Summary, Experience, Education }
}

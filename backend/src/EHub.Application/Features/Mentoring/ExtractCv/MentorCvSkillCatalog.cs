using System.Text.RegularExpressions;
using EHub.Application.Features.Mentoring.Evidence;

namespace EHub.Application.Features.Mentoring.ExtractCv;

/// <summary>Conservative normalization of explicit professional facts; semantic fit is evaluated separately by AI.</summary>
internal static class MentorCvSkillCatalog
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private sealed record Keyword(string Field, string Value, Regex Pattern);
    private static readonly Keyword[] Keywords = Build("expertise",
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
    ]).Concat(Build("startupDomain",
    [
        ("FinTech", ["fintech", "financial technology", "cong nghe tai chinh"]),
        ("EdTech", ["edtech", "educational technology", "cong nghe giao duc"]),
        ("HealthTech", ["healthtech", "medical technology", "cong nghe y te"]),
        ("AgriTech", ["agritech", "agricultural technology", "cong nghe nong nghiep"]),
        ("E-commerce", ["e-commerce", "ecommerce", "thuong mai dien tu"]),
        ("Logistics", ["logistics", "supply chain", "chuoi cung ung", "hau can"]),
        ("Tourism", ["tourism", "travel industry", "du lich"]),
        ("Manufacturing", ["manufacturing", "san xuat cong nghiep"]),
        ("Energy", ["renewable energy", "solar energy", "nang luong"]),
        ("Environment", ["environmental", "waste management", "moi truong", "xu ly chat thai"]),
        ("Real estate", ["real estate", "proptech", "bat dong san"]),
        ("Retail", ["retail", "ban le"]),
        ("Food and beverage", ["food and beverage", "foodtech", "f&b", "am thuc", "thuc pham"]),
        ("Education", ["education", "giao duc", "dao tao"]),
        ("Healthcare", ["healthcare", "health care", "cham soc suc khoe", "y te"]),
        ("Agriculture", ["agriculture", "agricultural", "nong nghiep"]),
        ("Finance", ["finance", "financial services", "banking", "tai chinh", "ngan hang"]),
        ("HRTech", ["hrtech", "hr technology", "cong nghe nhan su"]),
        ("LegalTech", ["legaltech", "legal technology", "cong nghe phap ly"])
    ])).Concat(Build("technology",
    [
        ("React", ["react", "reactjs", "react.js"]),
        ("React Native", ["react native"]),
        ("Angular", ["angular"]), ("Vue", ["vue", "vue.js", "vuejs"]),
        ("Next.js", ["next.js", "nextjs"]), ("Node.js", ["node.js", "nodejs"]),
        (".NET", [".net", "dotnet", "asp.net", "asp.net core"]),
        ("C#", ["c#", "csharp"]), ("C++", ["c++"]),
        ("Java", ["java"]), ("JavaScript", ["javascript"]),
        ("TypeScript", ["typescript"]), ("Python", ["python"]),
        ("Go", ["golang"]), ("Rust", ["rust"]),
        ("Flutter", ["flutter"]), ("Kotlin", ["kotlin"]), ("Swift", ["swift"]),
        ("SQL", ["sql"]), ("PostgreSQL", ["postgresql", "postgres"]),
        ("MySQL", ["mysql"]), ("SQL Server", ["sql server", "mssql"]),
        ("MongoDB", ["mongodb"]), ("Redis", ["redis"]),
        ("Docker", ["docker"]), ("Kubernetes", ["kubernetes", "k8s"]),
        ("AWS", ["aws", "amazon web services"]), ("Azure", ["azure"]),
        ("Google Cloud", ["google cloud", "gcp"]),
        ("Machine learning", ["machine learning", "hoc may"]),
        ("Computer vision", ["computer vision", "thi giac may tinh"]),
        ("NLP", ["nlp", "natural language processing", "xu ly ngon ngu tu nhien"]),
        ("IoT", ["iot", "internet of things", "internet van vat"]),
        ("Blockchain", ["blockchain"]), ("Data engineering", ["data engineering", "ky thuat du lieu"]),
        ("Cybersecurity", ["cybersecurity", "an toan thong tin"]),
        ("TensorFlow", ["tensorflow"]), ("PyTorch", ["pytorch"]),
        ("Figma", ["figma"]), ("Git", ["git"]),
        ("NestJS", ["nestjs", "nest.js"]), ("Django", ["django"]),
        ("Spring Boot", ["spring boot"]), ("PHP", ["php"]), ("Laravel", ["laravel"])
    ])).ToArray();

    internal static IReadOnlyList<MentorCvProfileEvidence> Extract(IEnumerable<string> professionalLines)
    {
        var result = new List<MentorCvProfileEvidence>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in professionalLines)
        {
            // A degree remains education even when the CV places it under a general summary heading.
            if (Regex.IsMatch(MentorEvidenceText.Normalize(line),
                @"\b(?:(?:bachelor|master)(?:'s)?\s+(?:degree|of|in)|degree in|bsc|msc|ph\.?d|diploma|cu nhan|thac si|tien si|tot nghiep)\b",
                RegexOptions.CultureInvariant, Timeout)) continue;
            foreach (var clause in Regex.Split(line, @";|[!?]|\.(?=\s|$)|\b(?:nhưng|nhung|tuy nhiên|tuy nhien|but|however|instead)\b",
                         RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, Timeout))
            {
                // Do not turn an affirmative word inside a denial into a positive credential.
                if (MentorEvidenceText.Excluded(clause).Length != 0) continue;
                foreach (var quote in Fragments(clause.Trim()))
                {
                    var normalized = MentorEvidenceText.Positive(quote);
                    foreach (var keyword in Keywords)
                    {
                        if (counts.GetValueOrDefault(keyword.Field) >= 20 ||
                            !keyword.Pattern.IsMatch(normalized) || !seen.Add(keyword.Field + ":" + keyword.Value)) continue;
                        result.Add(new(keyword.Field, keyword.Value, quote));
                        counts[keyword.Field] = counts.GetValueOrDefault(keyword.Field) + 1;
                    }
                }
            }
        }
        return result;
    }

    private static IEnumerable<string> Fragments(string text)
    {
        while (text.Length > 0)
        {
            var length = Math.Min(text.Length, 320);
            if (length < text.Length)
            {
                var boundary = text.LastIndexOf(' ', length - 1, length);
                if (boundary > 0) length = boundary;
                else if (char.IsHighSurrogate(text[length - 1])) length--;
            }
            var quote = text[..length].Trim();
            if (quote.Length > 0) yield return quote;
            text = text[length..].TrimStart();
        }
    }

    private static IEnumerable<Keyword> Build(string field, (string Value, string[] Aliases)[] entries) =>
        entries.Select(entry => new Keyword(field, entry.Value, new Regex(
            @"(?<![\p{L}\p{N}+#.])(?:" + string.Join('|', entry.Aliases.Select(Regex.Escape)) + @")(?![\p{L}\p{N}+#.])",
            RegexOptions.CultureInvariant | RegexOptions.Compiled, Timeout)));
}

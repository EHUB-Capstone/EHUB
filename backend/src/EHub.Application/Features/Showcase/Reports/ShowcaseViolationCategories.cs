using EHub.Domain.Enums;
using EHub.Shared.Errors;

namespace EHub.Application.Features.Showcase.Reports;

internal static class ShowcaseViolationCategories
{
    public static ShowcaseViolationCategory FromReason(ShowcaseReportReason reason) => reason switch
    {
        ShowcaseReportReason.Spam => ShowcaseViolationCategory.Spam,
        ShowcaseReportReason.InappropriateContent => ShowcaseViolationCategory.AdultContent,
        _ => ShowcaseViolationCategory.Insult
    };

    public static ShowcaseReportReason ToReason(ShowcaseViolationCategory category) => category switch
    {
        ShowcaseViolationCategory.Spam => ShowcaseReportReason.Spam,
        ShowcaseViolationCategory.AdultContent => ShowcaseReportReason.InappropriateContent,
        _ => ShowcaseReportReason.Harassment
    };

    public static string Message(ShowcaseViolationCategory category) => category switch
    {
        ShowcaseViolationCategory.Profanity => "Profane language is not allowed.",
        ShowcaseViolationCategory.Insult => "Insults and personal attacks are not allowed.",
        ShowcaseViolationCategory.Threat => "Threats and incitement to violence are not allowed.",
        ShowcaseViolationCategory.Defamation => "Abusive accusations and defamatory language are not allowed.",
        ShowcaseViolationCategory.Racism => "Racist and racially derogatory language is not allowed.",
        ShowcaseViolationCategory.AdultContent => "Sexually explicit or inappropriate adult content is not allowed.",
        ShowcaseViolationCategory.Spam => "Spam and prohibited promotional content are not allowed.",
        _ => "Please remove prohibited language before submitting."
    };

    public static Error ErrorFor(IEnumerable<ShowcaseViolationCategory> categories)
    {
        var groups = categories.Distinct().Order().ToArray();
        // The bounded suffix carries all matched groups through existing Result/Error contracts.
        return new Error(ErrorCodes.ShowcaseLanguageBlocked + "_" + string.Join("_", groups.Select(x => x.ToString().ToUpperInvariant())),
            string.Join(" ", groups.Select(Message)));
    }
}

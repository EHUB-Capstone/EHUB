using System.Net.Mail;
using System.Text.RegularExpressions;

namespace EHub.Application.Features.Mentoring.UploadCv;

internal sealed record MentorCvEmails(string[] Values, bool IsAmbiguous);

internal static class MentorCvEmailParser
{
    private const int MaximumDistinctEmails = 20;
    private static readonly Regex EmailPattern = new(
        @"(?<![\p{L}\p{N}.!#$%&'*+/=?^_`{|}~@-])[a-z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+(?![\p{L}\p{N}@-]|\.\.)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static MentorCvEmails Parse(string text)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (Match match in EmailPattern.Matches(text))
            {
                // CV form fields may wrap the address in quotation marks. Keep
                // apostrophes inside a mailbox (o'connor), while removing its wrapper.
                var candidate = match.Value.Trim('\'');
                var local = candidate[..candidate.IndexOf('@')];
                if (candidate.Length > 320 || local.StartsWith('.') || local.EndsWith('.') || local.Contains("..") ||
                    !MailAddress.TryCreate(candidate, out var address) ||
                    !string.Equals(candidate, address.Address, StringComparison.OrdinalIgnoreCase)) continue;
                values.Add(candidate.ToLowerInvariant());
                // Never infer uniqueness from a truncated list of addresses.
                if (values.Count > MaximumDistinctEmails)
                    return new MentorCvEmails(values.Take(MaximumDistinctEmails).ToArray(), true);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // The input is private CV text: do not log the exception or return its contents.
            return new MentorCvEmails(values.Take(MaximumDistinctEmails).ToArray(), true);
        }
        return new MentorCvEmails(values.ToArray(), false);
    }
}

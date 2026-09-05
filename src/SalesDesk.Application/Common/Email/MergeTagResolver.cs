using System.Text.RegularExpressions;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Common.Email;

/// <summary>
/// Resolves the `{{Customer.Name}}`-style merge tags a template's ContentHtml can
/// contain (see Template.ContentHtml) against a real document, mirroring the
/// frontend's editor-preview resolver (core/utils/merge-tags.ts) — that one only
/// ever substitutes mock data for the live preview while authoring a template;
/// this is what substitutes real data when the template's body actually goes out
/// in a delivered email. Same guardrail as the frontend: an unrecognized token
/// resolves to an empty string, never a literal `{{...}}` left in the output.
/// </summary>
internal static class MergeTagResolver
{
    private static readonly Regex TagPattern = new(@"\{\{\s*([\w.]+)\s*\}\}", RegexOptions.Compiled);

    public static string Resolve(string html, Document document)
    {
        var customer = document.Customer;
        var values = new Dictionary<string, string>
        {
            ["Customer.Name"] = customer?.Name ?? string.Empty,
            ["Customer.Email"] = customer?.Email ?? string.Empty,
            ["Customer.Company"] = customer?.Company ?? string.Empty,
            ["Document.Number"] = document.DocumentNumber,
            ["Document.IssueDate"] = document.IssueDate.ToString("MMM d, yyyy"),
            ["Document.DueDate"] = document.DueDate.ToString("MMM d, yyyy"),
            ["Document.Total"] = CurrencyFormatter.Format(document.Total, document.Currency)
        };

        return TagPattern.Replace(html, match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty);
    }
}

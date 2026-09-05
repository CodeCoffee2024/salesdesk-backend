using System.Text;
using System.Text.RegularExpressions;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Common.Email;

/// <summary>
/// Renders a document's activity timeline (see DocumentActivity) as an HTML list
/// for the bottom of a notification email — the same underlying data and label
/// wording as the frontend's app-document-timeline component
/// (shared/document-timeline/document-timeline.component.ts), duplicated here
/// since email HTML can't reach into the Angular app to render it. Keeps the two
/// in sync by design intent; a label added to one should be added to the other.
/// </summary>
internal static class DocumentActivityEmailFormatter
{
    // Mirrors PublicDocumentMapper's exclusion list exactly — an email that goes
    // to the client must never show them the drafting event (they had no link to
    // a document that was never sent) or reminder-log noise (they already got
    // that as its own email).
    private static readonly HashSet<DocumentActivityType> ExcludedForClient =
    [
        DocumentActivityType.Created,
        DocumentActivityType.ReminderSent
    ];

    /// <summary>
    /// Builds the "Activity so far" HTML block, or an empty string if there's
    /// nothing to show yet — callers can splice the result straight into an
    /// email body without an extra null/empty check. Timestamps are localized
    /// into <paramref name="timeZoneId"/> (the sending workspace's own time zone,
    /// see Workspace.TimeZoneId) rather than shown as raw UTC — an email can't
    /// run JavaScript to adapt to each reader's own clock the way the web app's
    /// timeline does, so the workspace's own time zone is the next best fixed
    /// choice, labeled explicitly rather than left ambiguous.
    /// </summary>
    public static string BuildTimelineHtml(IEnumerable<DocumentActivity> activities, bool forClient, string timeZoneId)
    {
        var ordered = activities
            .Where(a => !forClient || !ExcludedForClient.Contains(a.Type))
            .OrderBy(a => a.OccurredAtUtc)
            .ToList();

        if (ordered.Count == 0)
        {
            return string.Empty;
        }

        var timeZone = TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var found) ? found : TimeZoneInfo.Utc;

        var rows = new StringBuilder();
        foreach (var activity in ordered)
        {
            var label = LabelFor(activity.Type, forClient);
            var detail = DetailFor(activity, forClient);
            var detailHtml = detail is null ? "" : $" &mdash; {System.Net.WebUtility.HtmlEncode(detail)}";
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(activity.OccurredAtUtc, timeZone);
            var zoneLabel = ZoneLabel(timeZone, localTime);

            rows.Append($"""
                <li style="margin-bottom:6px;"><strong>{localTime:MMM d, h:mm tt} {zoneLabel}</strong> &mdash; {label}{detailHtml}</li>
                """);
        }

        return $"""
            <p style="font-size:12px;font-weight:700;letter-spacing:0.04em;text-transform:uppercase;color:#6b7370;margin:24px 0 8px;">Activity so far</p>
            <ul style="margin:0;padding-left:18px;font-size:13px;color:#14192b;">
              {rows}
            </ul>
            """;
    }

    /// <summary>
    /// A short, human label for the zone a timestamp was just converted into —
    /// e.g. "PST"/"PDT" for America/Los_Angeles, "UTC" for UTC itself. Derived
    /// from TimeZoneInfo's own (Standard/Daylight)Name rather than a hardcoded
    /// abbreviation table, by taking the initials of that name's words (the
    /// convention essentially every such name follows: "Pacific Standard Time"
    /// -&gt; PST). Falls back to the numeric UTC offset for the rare zone whose
    /// name doesn't reduce to a recognizable abbreviation this way.
    /// </summary>
    private static string ZoneLabel(TimeZoneInfo timeZone, DateTime localTime)
    {
        if (timeZone.Id == "UTC")
        {
            return "UTC";
        }

        var name = timeZone.IsDaylightSavingTime(localTime) ? timeZone.DaylightName : timeZone.StandardName;
        var initials = new string(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => char.IsUpper(word[0]))
            .Select(word => word[0])
            .ToArray());

        if (initials.Length is >= 2 and <= 5)
        {
            return initials;
        }

        var offset = timeZone.GetUtcOffset(localTime);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
    }

    private static string LabelFor(DocumentActivityType type, bool forClient) => type switch
    {
        DocumentActivityType.Created => "Document created",
        DocumentActivityType.Dispatched => forClient ? "Sent to you" : "Sent to the client",
        DocumentActivityType.Viewed => forClient ? "You viewed this" : "Client viewed the document",
        DocumentActivityType.RevisionRequested => forClient ? "You requested changes" : "Client requested changes",
        DocumentActivityType.Edited => forClient ? "Updated by the studio" : "Document details updated",
        DocumentActivityType.Signed => forClient ? "You signed this" : "Signed by the client",
        DocumentActivityType.StatusChanged => forClient ? "Status updated" : "Status changed",
        DocumentActivityType.ReminderSent => "Reminder email sent",
        _ => type.ToString()
    };

    private static string? DetailFor(DocumentActivity activity, bool forClient)
    {
        if (string.IsNullOrEmpty(activity.Detail))
        {
            return null;
        }

        return activity.Type switch
        {
            DocumentActivityType.RevisionRequested => $"\"{activity.Detail}\"",
            DocumentActivityType.Signed => forClient ? null : $"by {activity.Detail}",
            DocumentActivityType.StatusChanged or DocumentActivityType.ReminderSent or DocumentActivityType.Created => Prettify(activity.Detail),
            _ => activity.Detail
        };
    }

    /// <summary>"InvoiceOverdueFirstNotice" -&gt; "Invoice Overdue First Notice" — matches the frontend's fallback so a new enum value never needs a matching display string here.</summary>
    private static string Prettify(string value) => Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");
}

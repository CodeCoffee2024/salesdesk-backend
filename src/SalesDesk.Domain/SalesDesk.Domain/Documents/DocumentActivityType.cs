namespace SalesDesk.Domain.Documents;

/// <summary>
/// What kind of event a <see cref="DocumentActivity"/> entry records. Distinct
/// from <see cref="DocumentStatus"/> — several activity types (Viewed, Edited)
/// don't correspond to any status transition at all, and a single status
/// transition (Dispatch) is reachable from more than one activity trigger
/// (create-and-send, edit-and-resend, the standalone "Mark as Sent" action).
/// </summary>
public enum DocumentActivityType
{
    /// <summary>The document was created as a Draft. Never shown on the public timeline — a client has no link to a document that's never been sent.</summary>
    Created,

    /// <summary>Sent (or re-sent) to the client — Draft/RevisionRequested → Sent.</summary>
    Dispatched,

    /// <summary>The client opened their public document link. Recorded every time, unlike Document.FirstViewedAtUtc which only remembers the first.</summary>
    Viewed,

    /// <summary>The client asked for changes from the public view.</summary>
    RevisionRequested,

    /// <summary>The workspace edited the document's content (template/due date/line items) while it was Draft or RevisionRequested.</summary>
    Edited,

    /// <summary>The client e-signed and accepted the document.</summary>
    Signed,

    /// <summary>A lifecycle status the workspace set directly (Mark as accepted/paid) rather than through a client action.</summary>
    StatusChanged,

    /// <summary>An automated payment/follow-up reminder email was sent (TASK-025).</summary>
    ReminderSent
}

namespace SalesDesk.Domain.Documents;

/// <summary>
/// How a client produced their signature — kept on the record itself since it's
/// part of the legal audit trail, not just a UI detail.
/// </summary>
public enum SignatureType
{
    Drawn,
    Typed
}

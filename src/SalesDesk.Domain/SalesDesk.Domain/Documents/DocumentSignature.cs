using SalesDesk.Domain.Common;

namespace SalesDesk.Domain.Documents;

/// <summary>
/// The e-signature captured when a client accepts a <see cref="Document"/> from its
/// public view. One per document — <see cref="Document.ApplySignature"/> is the only
/// place this is created, and its presence is what locks the document from further
/// edits (TASK-024 guardrail). Carries the full legal audit trail (signer identity,
/// IP/User-Agent, timestamp, and a hash of the document content at the moment of
/// signing) rather than just the image, so the record can later prove what was agreed to.
/// </summary>
public sealed class DocumentSignature : Entity
{
    public Guid DocumentId { get; private set; }

    public Document? Document { get; private set; }

    public string SignerName { get; private set; }

    public string SignerEmail { get; private set; }

    public SignatureType Type { get; private set; }

    /// <summary>PNG data URL — a drawn stroke trace or a rasterized typed/cursive rendering; both are stored in the same uniform image format.</summary>
    public string SignatureImageDataUrl { get; private set; }

    public string IpAddress { get; private set; }

    public string UserAgent { get; private set; }

    public DateTimeOffset SignedAtUtc { get; private set; }

    /// <summary>SHA-256 hex digest of the document's content (see <see cref="Document.ComputeContentHash"/>) at the instant of signing, proving what was accepted.</summary>
    public string DocumentHash { get; private set; }

    private DocumentSignature()
    {
        SignerName = string.Empty;
        SignerEmail = string.Empty;
        SignatureImageDataUrl = string.Empty;
        IpAddress = string.Empty;
        UserAgent = string.Empty;
        DocumentHash = string.Empty;
    }

    internal DocumentSignature(
        Guid documentId,
        string signerName,
        string signerEmail,
        SignatureType type,
        string signatureImageDataUrl,
        string ipAddress,
        string userAgent,
        DateTimeOffset signedAtUtc,
        string documentHash)
    {
        DocumentId = Guard.AgainstEmpty(documentId, nameof(documentId));
        SignerName = Guard.AgainstNullOrWhiteSpace(signerName, nameof(signerName));
        SignerEmail = Guard.AgainstNullOrWhiteSpace(signerEmail, nameof(signerEmail));
        Type = type;
        SignatureImageDataUrl = Guard.AgainstNullOrWhiteSpace(signatureImageDataUrl, nameof(signatureImageDataUrl));
        IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress;
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? "unknown" : userAgent;
        SignedAtUtc = signedAtUtc;
        DocumentHash = Guard.AgainstNullOrWhiteSpace(documentHash, nameof(documentHash));
    }
}

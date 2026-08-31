using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>
/// Backs POST /api/public/documents/{token}/signature — the client's "Accept &amp;
/// sign" action (TASK-024). IpAddress/UserAgent are captured server-side from the
/// request by the controller, not trusted from the request body, since they're part
/// of the legal audit trail.
/// </summary>
public sealed record SignDocumentCommand(
    Guid Token,
    string SignerName,
    string SignerEmail,
    bool AgreedToTerms,
    SignatureType SignatureType,
    string SignatureImageDataUrl,
    string IpAddress,
    string UserAgent) : IRequest<PublicDocumentDto>;

public sealed class SignDocumentCommandValidator : AbstractValidator<SignDocumentCommand>
{
    // A drawn/rasterized PNG data URL comfortably fits in a few hundred KB; this
    // caps well above that to stop a pathological payload without being a real
    // constraint on a legitimate signature.
    private const int MaxSignatureImageLength = 2_000_000;

    public SignDocumentCommandValidator()
    {
        RuleFor(c => c.SignerName).NotEmpty().MaximumLength(200);
        RuleFor(c => c.SignerEmail).NotEmpty().EmailAddress();
        RuleFor(c => c.AgreedToTerms).Equal(true).WithMessage("You must agree to the terms to sign this document.");
        RuleFor(c => c.SignatureImageDataUrl)
            .NotEmpty()
            .MaximumLength(MaxSignatureImageLength)
            .Must(value => value.StartsWith("data:image/png;base64,", StringComparison.Ordinal))
            .WithMessage("Signature image must be a PNG data URL.");
    }
}

public sealed class SignDocumentCommandHandler(IApplicationDbContext context, IDateTime dateTime)
    : IRequestHandler<SignDocumentCommand, PublicDocumentDto>
{
    public async Task<PublicDocumentDto> Handle(SignDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await context.Documents
            .Include(d => d.Customer)
            .Include(d => d.LineItems)
            .Include(d => d.Signature)
            .FirstOrDefaultAsync(d => d.PublicToken == request.Token, cancellationToken)
            ?? throw new NotFoundException(nameof(Document), request.Token);

        var signature = document.ApplySignature(
            request.SignerName,
            request.SignerEmail,
            request.SignatureType,
            request.SignatureImageDataUrl,
            request.IpAddress,
            request.UserAgent,
            dateTime.UtcNow);

        // DocumentSignature.Id is a client-generated Guid (never left at its CLR
        // default), so reaching it only via navigation fixup on an already-tracked
        // (Unchanged) Document leaves EF Core's DetectChanges unable to tell it
        // apart from an existing row — it issues an UPDATE for a row that was
        // never inserted (0 rows affected, DbUpdateConcurrencyException). Same
        // failure mode as UpdateDocumentCommand's line-item replace; same fix.
        context.DocumentSignatures.Add(signature);

        await context.SaveChangesAsync(cancellationToken);

        var workspace = await context.Workspaces.FirstAsync(w => w.Id == document.WorkspaceId, cancellationToken);

        return PublicDocumentMapper.ToDto(document, workspace.Name, workspace.LogoUrl);
    }
}

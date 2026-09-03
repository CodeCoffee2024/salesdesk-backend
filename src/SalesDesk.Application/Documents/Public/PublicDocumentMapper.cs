using SalesDesk.Domain.Documents;

namespace SalesDesk.Application.Documents.Public;

/// <summary>
/// Hand-rolled instead of an AutoMapper profile: <see cref="PublicDocumentDto"/>
/// needs the issuing workspace's name/logo folded in alongside the document, which
/// don't live on <see cref="Document"/> itself — a couple of ForMember(.MapFrom)
/// lambdas would obscure that more than they'd save.
/// </summary>
internal static class PublicDocumentMapper
{
    public static PublicDocumentDto ToDto(Document document, string workspaceName, string? workspaceLogoUrl) =>
        new()
        {
            DocumentNumber = document.DocumentNumber,
            Type = document.Type,
            Status = document.Status,
            IssueDate = document.IssueDate,
            DueDate = document.DueDate,
            CustomerName = document.Customer?.Name ?? string.Empty,
            CustomerCompany = document.Customer?.Company ?? string.Empty,
            WorkspaceName = workspaceName,
            WorkspaceLogoUrl = workspaceLogoUrl,
            Subtotal = document.Subtotal,
            Total = document.Total,
            Currency = document.Currency,
            ClientCountry = document.ClientCountry,
            LineItems = document.LineItems.Select(li => new DocumentLineItemDto
            {
                Id = li.Id,
                ProductId = li.ProductId,
                Description = li.Description,
                Quantity = li.Quantity,
                UnitPrice = li.UnitPrice,
                LineTotal = li.LineTotal
            }).ToList(),
            IsSigned = document.Signature is not null,
            SignedByName = document.Signature?.SignerName,
            SignedAtUtc = document.Signature?.SignedAtUtc,
            SignatureImageDataUrl = document.Signature?.SignatureImageDataUrl
        };
}

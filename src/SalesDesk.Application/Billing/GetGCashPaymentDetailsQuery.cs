using MediatR;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Billing;

/// <summary>TASK-039: what the "Pay via GCash" modal needs to render — the platform's own receiving account details plus the PH pricing tiers to quote an exact amount due for Pro/Studio, Monthly/Annual.</summary>
public sealed record GCashPaymentDetailsDto(string? AccountName, string? MobileNumber, string? QrCodeUrl, List<PricingTierDto> Tiers);

public sealed record GetGCashPaymentDetailsQuery : IRequest<GCashPaymentDetailsDto>;

public sealed class GetGCashPaymentDetailsQueryHandler(IBillingSettings billingSettings)
    : IRequestHandler<GetGCashPaymentDetailsQuery, GCashPaymentDetailsDto>
{
    public Task<GCashPaymentDetailsDto> Handle(GetGCashPaymentDetailsQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(new GCashPaymentDetailsDto(
            billingSettings.AccountName,
            billingSettings.MobileNumber,
            billingSettings.QrCodeUrl,
            PricingCatalog.ForCountry("PH").Tiers.Where(t => t.Tier != "Free").ToList()));
}

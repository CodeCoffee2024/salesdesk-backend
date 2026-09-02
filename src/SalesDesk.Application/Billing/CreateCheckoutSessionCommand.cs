using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Exceptions;
using SalesDesk.Application.Common.Extensions;
using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Billing;

/// <summary>TASK-038: "Upgrade" on /settings/billing. Tier is "Pro" or "Studio" (never "Free" — nothing to check out for that); BillingCycle is "Monthly" or "Annual".</summary>
public sealed record CreateCheckoutSessionCommand(string Tier, string BillingCycle) : IRequest<CheckoutSession>;

public sealed class CreateCheckoutSessionCommandValidator : AbstractValidator<CreateCheckoutSessionCommand>
{
    public CreateCheckoutSessionCommandValidator()
    {
        RuleFor(c => c.Tier).Must(t => t is "Pro" or "Studio").WithMessage("Tier must be Pro or Studio.");
        RuleFor(c => c.BillingCycle).Must(c => c is "Monthly" or "Annual").WithMessage("BillingCycle must be Monthly or Annual.");
    }
}

public sealed class CreateCheckoutSessionCommandHandler(
    IApplicationDbContext context, ICurrentUserService currentUser, IPaymentGatewayService paymentGateway)
    : IRequestHandler<CreateCheckoutSessionCommand, CheckoutSession>
{
    public async Task<CheckoutSession> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var workspaceId = currentUser.RequireWorkspaceId();
        var workspace = await context.Workspaces.SingleAsync(w => w.Id == workspaceId, cancellationToken);

        var catalog = PricingCatalog.ForCountry(workspace.Country);
        var pricedTier = catalog.Tiers.SingleOrDefault(t => t.Tier == request.Tier)
            ?? throw new InvalidOperationException($"Unknown tier '{request.Tier}'.");

        var amount = request.BillingCycle == "Annual" ? pricedTier.AnnualPrice : pricedTier.MonthlyPrice;

        return await paymentGateway.CreateCheckoutSessionAsync(
            workspaceId, pricedTier.Tier, request.BillingCycle, catalog.Currency, amount, cancellationToken);
    }
}

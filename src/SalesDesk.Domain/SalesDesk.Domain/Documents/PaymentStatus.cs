namespace SalesDesk.Domain.Documents;

/// <summary>
/// Whether an invoice has actually been paid online (TASK-042), independent of
/// <see cref="DocumentStatus"/> — a document can be manually marked
/// <see cref="DocumentStatus.Paid"/> by a workspace owner with no real payment
/// behind it, so this tracks the online-payment fact specifically. No
/// PartiallyPaid value: Stripe Checkout here is a single full-amount charge, so
/// there is no code path that can produce a partial payment.
/// </summary>
public enum PaymentStatus
{
    Unpaid,
    Paid
}

namespace SalesDesk.Application.Dashboard;

public sealed class DashboardSummaryDto
{
    /// <summary>Sum of paid invoice totals issued in the current calendar year.</summary>
    public decimal RevenueThisYear { get; init; }

    /// <summary>Sum of totals for invoices that have been sent but not yet paid (Sent or Overdue).</summary>
    public decimal Outstanding { get; init; }

    /// <summary>Sum of totals for quotes still waiting on a customer decision (Draft or Sent).</summary>
    public decimal QuotePipeline { get; init; }

    /// <summary>Distinct customers with at least one document issued in the current calendar quarter.</summary>
    public int ActiveCustomers { get; init; }

    /// <summary>
    /// ISO 4217 code every amount above has been normalized into — the workspace's
    /// own DefaultCurrency (TASK-029). Documents issued in a different currency are
    /// converted via <see cref="Common.Interfaces.ICurrencyConversionService"/>
    /// before being summed, so these totals are directly comparable even when the
    /// workspace issues quotes/invoices internationally.
    /// </summary>
    public string BaseCurrency { get; init; } = "USD";
}

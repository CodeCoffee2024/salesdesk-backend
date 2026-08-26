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
}

namespace SalesDesk.Application.Reports;

public sealed class RevenueReportDto
{
    public List<RevenuePointDto> Points { get; init; } = [];

    /// <summary>Total owed on Sent/Overdue invoices issued within the selected range, converted into BaseCurrency.</summary>
    public decimal Outstanding { get; init; }

    public string BaseCurrency { get; init; } = "USD";
}

public sealed class RevenuePointDto
{
    /// <summary>"Jan 5" for a daily bucket, "Jan 2026" for a monthly one — see GetRevenueReportQueryHandler.</summary>
    public string Label { get; init; } = string.Empty;

    public decimal Amount { get; init; }
}

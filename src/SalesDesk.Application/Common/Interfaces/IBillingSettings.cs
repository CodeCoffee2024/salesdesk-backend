namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// TASK-039: platform-level GCash/billing configuration — never per-workspace, so
/// it lives in appsettings/config rather than on any entity. Implemented in
/// Infrastructure (reads IConfiguration) so Application stays config-agnostic, the
/// same boundary IEmailSender/IPaymentGatewayService already keep.
/// </summary>
public interface IBillingSettings
{
    /// <summary>Where a new GCash payment submission's admin notification goes. Null/empty means nobody's configured yet — the submission still saves, there's just no one to page.</summary>
    string? AdminNotificationEmail { get; }

    /// <summary>The platform's own GCash account name, shown to a submitter alongside the mobile number and QR code.</summary>
    string? AccountName { get; }

    /// <summary>The platform's own GCash-registered mobile number.</summary>
    string? MobileNumber { get; }

    /// <summary>A hosted image URL for the platform's own GCash QR code, or null if none is configured yet.</summary>
    string? QrCodeUrl { get; }
}

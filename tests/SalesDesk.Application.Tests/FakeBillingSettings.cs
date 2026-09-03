using SalesDesk.Application.Common.Interfaces;

namespace SalesDesk.Application.Tests;

public sealed class FakeBillingSettings : IBillingSettings
{
    public string? AdminNotificationEmail { get; set; }

    public string? AccountName { get; set; } = "SalesDesk PH";

    public string? MobileNumber { get; set; } = "09171234567";

    public string? QrCodeUrl { get; set; } = "https://cdn.example.test/gcash-qr.png";
}

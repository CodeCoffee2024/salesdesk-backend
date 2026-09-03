using Microsoft.EntityFrameworkCore;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Billing;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Promotions;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Application.Common.Interfaces;

/// <summary>
/// The persistence surface Application handlers depend on. Keeping this interface
/// in Application (implemented by <c>SalesDeskDbContext</c> in Infrastructure) lets
/// handlers query and save data without Application referencing Infrastructure or
/// Npgsql directly, preserving the Clean Architecture dependency direction.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Workspace> Workspaces { get; }

    DbSet<User> Users { get; }

    DbSet<PushSubscription> PushSubscriptions { get; }

    DbSet<Customer> Customers { get; }

    DbSet<Product> Products { get; }

    DbSet<Template> Templates { get; }

    DbSet<Document> Documents { get; }

    DbSet<DocumentLineItem> DocumentLineItems { get; }

    DbSet<DocumentSignature> DocumentSignatures { get; }

    DbSet<DocumentActivity> DocumentActivities { get; }

    DbSet<DocumentReminderLog> DocumentReminderLogs { get; }

    DbSet<ReminderSettings> ReminderSettingsEntries { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<PromoCounter> PromoCounters { get; }

    DbSet<GCashPaymentSubmission> GCashPaymentSubmissions { get; }

    DbSet<SubscriptionUpgradeRequest> SubscriptionUpgradeRequests { get; }

    /// <summary>
    /// TASK-031: atomically claims the next "Early 100 Free Year" promo slot and
    /// returns whether this caller got one. Implemented as a single
    /// `UPDATE promo_counters SET count = count + 1 WHERE key = ... AND count &lt; 100`
    /// statement, executed immediately (not queued onto the caller's later
    /// SaveChangesAsync): the row-level lock the database takes for that
    /// statement's duration is what makes two concurrent registrations racing for
    /// the same slot safe — only one can affect the row before the other's WHERE
    /// clause is re-evaluated against the just-updated count, so at most
    /// PromoCounter.EarlyBirdCap callers ever see `true`, with no
    /// read-then-write window for a race to slip through. Once the cap is reached
    /// this simply returns false — the expected, tested fallback to standard
    /// freemium provisioning, not an error.
    /// </summary>
    Task<bool> TryReserveEarlyBirdPromoSlotAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

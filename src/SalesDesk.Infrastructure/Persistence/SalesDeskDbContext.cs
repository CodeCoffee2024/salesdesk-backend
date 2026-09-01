using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Promotions;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Infrastructure.Persistence;

public sealed class SalesDeskDbContext(DbContextOptions<SalesDeskDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<User> Users => Set<User>();

    public DbSet<PushSubscription> PushSubscriptions => Set<PushSubscription>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentLineItem> DocumentLineItems => Set<DocumentLineItem>();

    public DbSet<DocumentSignature> DocumentSignatures => Set<DocumentSignature>();

    public DbSet<DocumentReminderLog> DocumentReminderLogs => Set<DocumentReminderLog>();

    public DbSet<ReminderSettings> ReminderSettingsEntries => Set<ReminderSettings>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<PromoCounter> PromoCounters => Set<PromoCounter>();

    /// <inheritdoc cref="IApplicationDbContext.TryReserveEarlyBirdPromoSlotAsync"/>
    public async Task<bool> TryReserveEarlyBirdPromoSlotAsync(CancellationToken cancellationToken)
    {
        var rowsAffected = await Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE promo_counters SET count = count + 1 WHERE key = {PromoCounter.EarlyBirdPremiumKey} AND count < {PromoCounter.EarlyBirdCap}",
            cancellationToken);

        return rowsAffected > 0;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDeskDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

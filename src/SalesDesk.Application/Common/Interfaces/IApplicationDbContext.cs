using Microsoft.EntityFrameworkCore;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
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

    DbSet<Customer> Customers { get; }

    DbSet<Product> Products { get; }

    DbSet<Template> Templates { get; }

    DbSet<Document> Documents { get; }

    DbSet<DocumentLineItem> DocumentLineItems { get; }

    DbSet<DocumentSignature> DocumentSignatures { get; }

    DbSet<DocumentReminderLog> DocumentReminderLogs { get; }

    DbSet<ReminderSettings> ReminderSettingsEntries { get; }

    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

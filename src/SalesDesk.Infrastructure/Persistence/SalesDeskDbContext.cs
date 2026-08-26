using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Domain.Audit;
using SalesDesk.Domain.Customers;
using SalesDesk.Domain.Documents;
using SalesDesk.Domain.Products;
using SalesDesk.Domain.Templates;
using SalesDesk.Domain.Users;
using SalesDesk.Domain.Workspaces;

namespace SalesDesk.Infrastructure.Persistence;

public sealed class SalesDeskDbContext(DbContextOptions<SalesDeskDbContext> options) : DbContext(options), IApplicationDbContext
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Template> Templates => Set<Template>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentLineItem> DocumentLineItems => Set<DocumentLineItem>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesDeskDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Infrastructure.Persistence.Seed;
using SalesDesk.Infrastructure.Services;

namespace SalesDesk.Infrastructure.Tests;

public class SalesDeskDbContextSeederTests
{
    private static readonly PasswordHasher PasswordHasher = new();

    [Fact]
    public async Task SeedAsync_populates_the_expected_shape()
    {
        using var fixture = new SqliteDbContextFixture();

        await SalesDeskDbContextSeeder.SeedAsync(fixture.Context, PasswordHasher);

        // 4 workspaces: Northline (the main demo tenant), SalesDesk HQ (hosts the
        // seeded SystemAdmin — TASK-017), and two lightweight extra tenants used to
        // demonstrate the admin console's quota/suspension states.
        using var verify = fixture.CreateContext();
        (await verify.Workspaces.CountAsync()).Should().Be(4);
        (await verify.Users.CountAsync()).Should().Be(4);
        (await verify.Customers.CountAsync()).Should().Be(5);
        (await verify.Products.CountAsync()).Should().Be(4);
        (await verify.Templates.CountAsync()).Should().Be(4);
        (await verify.Templates.CountAsync(t => t.IsDefault)).Should().Be(2);
        (await verify.Documents.CountAsync()).Should().Be(10);
        (await verify.DocumentLineItems.CountAsync()).Should().Be(10);
        (await verify.Users.CountAsync(u => u.Role == Domain.Users.Role.SystemAdmin)).Should().Be(1);
        (await verify.Workspaces.CountAsync(w => !w.IsActive)).Should().Be(1);
    }

    [Fact]
    public async Task SeedAsync_does_not_duplicate_data_when_called_again()
    {
        using var fixture = new SqliteDbContextFixture();

        await SalesDeskDbContextSeeder.SeedAsync(fixture.Context, PasswordHasher);
        await SalesDeskDbContextSeeder.SeedAsync(fixture.Context, PasswordHasher);

        using var verify = fixture.CreateContext();
        (await verify.Workspaces.CountAsync()).Should().Be(4);
        (await verify.Customers.CountAsync()).Should().Be(5);
        (await verify.Documents.CountAsync()).Should().Be(10);
    }

    [Fact]
    public async Task SeedAsync_produces_documents_whose_totals_match_their_line_items()
    {
        using var fixture = new SqliteDbContextFixture();

        await SalesDeskDbContextSeeder.SeedAsync(fixture.Context, PasswordHasher);

        using var verify = fixture.CreateContext();
        var documents = await verify.Documents.Include(d => d.LineItems).ToListAsync();

        documents.Should().AllSatisfy(document =>
        {
            document.LineItems.Should().NotBeEmpty();
            document.Subtotal.Should().Be(document.LineItems.Sum(li => li.LineTotal));
            document.Total.Should().Be(document.Subtotal);
        });
    }

    [Fact]
    public async Task SeedAsync_gives_every_document_a_valid_customer_and_template_reference()
    {
        using var fixture = new SqliteDbContextFixture();

        await SalesDeskDbContextSeeder.SeedAsync(fixture.Context, PasswordHasher);

        using var verify = fixture.CreateContext();
        var customerIds = (await verify.Customers.Select(c => c.Id).ToListAsync()).ToHashSet();
        var templateIds = (await verify.Templates.Select(t => t.Id).ToListAsync()).ToHashSet();
        var documents = await verify.Documents.ToListAsync();

        documents.Should().AllSatisfy(document =>
        {
            customerIds.Should().Contain(document.CustomerId);
            templateIds.Should().Contain(document.TemplateId);
        });
    }
}

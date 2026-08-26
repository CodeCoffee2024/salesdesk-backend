using AutoMapper;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Application.Common.Interfaces;
using SalesDesk.Application.Common.Mappings;
using SalesDesk.Infrastructure.Persistence;

namespace SalesDesk.Application.Tests;

/// <summary>
/// Gives each handler test a private, isolated SQLite in-memory database (via the
/// real <see cref="SalesDeskDbContext"/>, exposed through the
/// <see cref="IApplicationDbContext"/> abstraction handlers actually depend on) plus
/// a real <see cref="IMapper"/> built from the production mapping profile — so
/// these tests exercise the same query/mapping logic the API runs, without needing
/// a mocking library or a live PostgreSQL instance.
///
/// <see cref="Context"/> is convenient for arranging seed data, but a handler under
/// test should usually run against a context from <see cref="CreateContext"/>
/// instead: in the real app, a handler always gets a fresh, empty-of-tracked-entities
/// scoped DbContext per request. Reusing the seeding context can make EF Core's
/// change tracker behave differently than it would in production (e.g. detecting an
/// in-memory "severed required relationship" before the database ever gets a
/// chance to reject it) — <see cref="CreateContext"/> avoids that mismatch.
/// </summary>
public sealed class SqliteApplicationDbContextFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<SalesDeskDbContext> _createdContexts = [];

    public IApplicationDbContext Context { get; }

    public IMapper Mapper { get; }

    public SqliteApplicationDbContextFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        var seedContext = CreateContext();
        seedContext.Database.EnsureCreated();
        Context = seedContext;

        Mapper = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>()).CreateMapper();
    }

    public SalesDeskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SalesDeskDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        var context = new SalesDeskDbContext(options);
        _createdContexts.Add(context);
        return context;
    }

    public void Dispose()
    {
        foreach (var context in _createdContexts)
        {
            context.Dispose();
        }

        _connection.Dispose();
    }
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SalesDesk.Infrastructure.Persistence;

namespace SalesDesk.Infrastructure.Tests;

/// <summary>
/// Gives each test its own private, isolated SQLite in-memory database built from
/// the same EF Core model <see cref="SalesDeskDbContext"/> maps to PostgreSQL —
/// including real foreign keys, the unique document-number index, and
/// cascade/restrict/set-null delete behavior — without needing a live PostgreSQL
/// instance to run these tests.
///
/// A SQLite ":memory:" database only lives as long as its connection stays open,
/// and only one <see cref="SalesDeskDbContext"/> can safely write at a time on that
/// connection. <see cref="CreateContext"/> lets a test open a second, independent
/// context against the same in-memory database — the same way a real request would
/// get its own <c>DbContext</c> — so assertions read back what was actually
/// persisted rather than what the original context still has cached in its change
/// tracker.
/// </summary>
public sealed class SqliteDbContextFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SalesDeskDbContext Context { get; }

    public SqliteDbContextFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var pragma = _connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        Context = CreateContext();
        Context.Database.EnsureCreated();
    }

    public SalesDeskDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SalesDeskDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new SalesDeskDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

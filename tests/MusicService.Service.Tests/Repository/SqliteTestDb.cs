using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MusicService.Repository.Data;

namespace MusicService.Service.Tests.Repository;

// AppDbContext on SQLite in memory: a real relational database, so keys, unique indexes,
// foreign keys and cascades are enforced like in Postgres. One database per test class instance.
public abstract class SqliteTestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;

    protected SqliteTestDb()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    // A fresh context, so reads do not see entities tracked by an earlier write.
    protected AppDbContext NewDb() => new(_options);

    public void Dispose() => _connection.Dispose();
}

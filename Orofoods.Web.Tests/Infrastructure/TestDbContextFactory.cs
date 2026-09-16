using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class TestDbContextFactory
{
    public static async Task<ApplicationDbContext> CreateAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }
}

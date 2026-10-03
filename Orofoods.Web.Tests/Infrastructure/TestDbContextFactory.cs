using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class TestDbContextFactory
{
    public static async Task<ApplicationDbContext> CreateAsync()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            ForeignKeys = true
        }.ToString());
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    public static ApplicationUser CreateOrderCreator(string id = "")
    {
        var userId = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id;
        return new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@test.local",
            NormalizedUserName = $"{userId}@TEST.LOCAL",
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@TEST.LOCAL",
            IsActive = true
        };
    }
}

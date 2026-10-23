using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class TestDbContextFactory
{
    public static async Task<ApplicationDbContext> CreateAsync(DbCommandInterceptor? commandInterceptor = null)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = ":memory:",
            ForeignKeys = true
        }.ToString());
        await connection.OpenAsync();
        connection.CreateFunction<string?, string, string, string?>(
            "translate",
            TranslateSearchText,
            isDeterministic: true);

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection);
        if (commandInterceptor is not null)
            optionsBuilder.AddInterceptors(commandInterceptor);
        var options = optionsBuilder.Options;

        var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static string? TranslateSearchText(string? value, string fromCharacters, string toCharacters)
    {
        if (value is null) return null;

        var translated = new System.Text.StringBuilder(value.Length);
        foreach (var character in value)
        {
            var index = fromCharacters.IndexOf(character);
            if (index < 0)
                translated.Append(character);
            else if (index < toCharacters.Length)
                translated.Append(toCharacters[index]);
        }

        return translated.ToString();
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

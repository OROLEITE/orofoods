using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Orofoods.Web.Data;

namespace Orofoods.Web.Tests.Infrastructure;

/// <summary>Provides a non-connecting SQLite model for scaffolding the test SQLite migration stream.</summary>
public sealed class SqliteApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=orofoods_sqlite_design_time.db")
            .Options;
        return new ApplicationDbContext(options);
    }
}

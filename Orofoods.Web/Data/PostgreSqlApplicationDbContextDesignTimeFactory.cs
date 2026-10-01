using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orofoods.Web.Data;

/// <summary>
/// Gives EF tooling a local, non-connecting PostgreSQL model context. Migration scaffolding
/// must not start the web host, seed data, or apply migrations to a configured runtime database.
/// </summary>
public sealed class PostgreSqlApplicationDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PostgreSqlApplicationDbContext>
{
    public PostgreSqlApplicationDbContext CreateDbContext(string[] args)
    {
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        var options = new DbContextOptionsBuilder<PostgreSqlApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=5432;Database=orofoods_design_time;Username=design_time")
            .Options;
        return new PostgreSqlApplicationDbContext(options);
    }
}

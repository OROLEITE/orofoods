using Microsoft.Extensions.Configuration;

namespace Orofoods.Web.Configuration;

internal static class DatabaseConfiguration
{
    internal static string GetRequiredDefaultConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException("Connection string 'DefaultConnection' is required.")
            : connectionString;
    }
}

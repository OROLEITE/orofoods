using System.Runtime.CompilerServices;

namespace Orofoods.Web.Tests;

internal static class SqliteProviderInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
        SQLitePCL.raw.FreezeProvider();
    }
}

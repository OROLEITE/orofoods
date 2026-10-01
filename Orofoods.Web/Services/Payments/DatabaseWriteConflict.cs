using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Orofoods.Web.Services.Payments;

internal static class DatabaseWriteConflict
{
    public static bool IsExpected(Exception exception)
    {
        if (exception is PostgresException postgres)
        {
            return postgres.SqlState is PostgresErrorCodes.UniqueViolation
                or PostgresErrorCodes.SerializationFailure
                or PostgresErrorCodes.DeadlockDetected;
        }

        if (exception.GetType().FullName == "Microsoft.Data.Sqlite.SqliteException")
        {
            var extendedCode = exception.GetType().GetProperty("SqliteExtendedErrorCode")?.GetValue(exception) as int?;
            var primaryCode = exception.GetType().GetProperty("SqliteErrorCode")?.GetValue(exception) as int?;
            return extendedCode is 1555 or 2067 or 5 or 6 || primaryCode is 5 or 6;
        }

        if (exception is DbUpdateException && exception.InnerException is not null)
        {
            return IsExpected(exception.InnerException);
        }

        return exception.InnerException is not null && IsExpected(exception.InnerException);
    }
}

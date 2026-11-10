using System.Text.Json;

namespace OrofoodsProductionDbReadOnly;

public static class ProductionDiagnosticContract
{
    public const string ExpectedEnvironment = "Production";
    public const string ExpectedHost = "psql-orofoods-prd-01.postgres.database.azure.com";
    public const string ExpectedDatabase = "orofoods";

    public const string ReadOnlyCommandText = "SET TRANSACTION READ ONLY;";
    public const string TransactionModeCommandText = "SHOW transaction_read_only;";
    public const string DatabaseCommandText = "SELECT current_database();";
    public const string MigrationHistoryCommandText =
        "SELECT \"MigrationId\" FROM public.\"__EFMigrationsHistory\" ORDER BY \"MigrationId\";";
    public const string DuplicatePhoneAggregateCommandText = """
        SELECT COUNT(*)::bigint AS "DuplicatePhoneGroups",
               COALESCE(SUM("RowCount"), 0)::bigint AS "AffectedConversationRows"
        FROM (
            SELECT COUNT(*)::bigint AS "RowCount"
            FROM public."WhatsAppConversations"
            GROUP BY "PhoneNumber"
            HAVING COUNT(*) > 1
        ) AS duplicates;
        """;

    public static string ReadOnlySql => string.Join(
        Environment.NewLine,
        ReadOnlyCommandText,
        TransactionModeCommandText,
        DatabaseCommandText,
        MigrationHistoryCommandText,
        DuplicatePhoneAggregateCommandText);

    public static bool IsProductionEnvironment(string? value) =>
        string.Equals(value, ExpectedEnvironment, StringComparison.Ordinal);

    public static bool IsExpectedHost(string? value) =>
        string.Equals(value?.TrimEnd('.'), ExpectedHost, StringComparison.OrdinalIgnoreCase);

    public static string[] LoadMigrationManifest(string manifestPath)
    {
        var migrationIds = JsonSerializer.Deserialize<string[]>(File.ReadAllText(manifestPath));
        if (migrationIds is null || migrationIds.Length == 0)
            throw new InvalidDataException();

        var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var migrationId in migrationIds)
        {
            if (!IsMigrationId(migrationId) || !uniqueIds.Add(migrationId))
                throw new InvalidDataException();
        }

        return uniqueIds.OrderBy(static id => id, StringComparer.Ordinal).ToArray();
    }

    public static IReadOnlyList<string> FormatMigrationStatuses(
        IEnumerable<string> projectMigrationIds,
        IEnumerable<string> appliedMigrationIds)
    {
        var projectIds = new HashSet<string>(projectMigrationIds, StringComparer.Ordinal);
        var appliedIds = new HashSet<string>(appliedMigrationIds, StringComparer.Ordinal);
        var lines = new List<string>(projectIds.Count + appliedIds.Count);

        foreach (var migrationId in projectIds.OrderBy(static id => id, StringComparer.Ordinal))
        {
            var status = appliedIds.Contains(migrationId) ? "APPLIED" : "PENDING";
            lines.Add($"{migrationId} {status}");
        }

        foreach (var migrationId in appliedIds
                     .Where(id => !projectIds.Contains(id))
                     .OrderBy(static id => id, StringComparer.Ordinal))
        {
            lines.Add($"{migrationId} APPLIED_NOT_IN_PROJECT");
        }

        return lines;
    }

    public static bool IsMigrationId(string? migrationId)
    {
        if (migrationId is null || migrationId.Length < 16 || migrationId[14] != '_')
            return false;

        for (var index = 0; index < 14; index++)
        {
            if (migrationId[index] is < '0' or > '9')
                return false;
        }

        for (var index = 15; index < migrationId.Length; index++)
        {
            var character = migrationId[index];
            if (!(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '_'))
                return false;
        }

        return true;
    }
}

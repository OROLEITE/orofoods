namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>Read-only column discovery for tables the WMC mirror doesn't document. Development/support tool only, never public.</summary>
public interface IWmcSchemaInspector
{
    Task<IReadOnlyList<WmcColumnInfo>> GetColumnsAsync(string tableName, CancellationToken cancellationToken = default);
}

public sealed record WmcColumnInfo(string ColumnName, int FieldType, int? Length, bool Nullable);

public sealed class WmcSchemaInspector(IWmcFirebirdReader reader) : IWmcSchemaInspector
{
    public Task<IReadOnlyList<WmcColumnInfo>> GetColumnsAsync(string tableName, CancellationToken cancellationToken = default) =>
        reader.QueryAsync(
            """
            SELECT rf.RDB$FIELD_NAME, f.RDB$FIELD_TYPE, f.RDB$FIELD_LENGTH, rf.RDB$NULL_FLAG
            FROM RDB$RELATION_FIELDS rf
            JOIN RDB$FIELDS f ON f.RDB$FIELD_NAME = rf.RDB$FIELD_SOURCE
            WHERE rf.RDB$RELATION_NAME = @table
            ORDER BY rf.RDB$FIELD_POSITION
            """,
            row => new WmcColumnInfo(
                row.GetString(0).Trim(),
                row.GetInt32(1),
                row.IsDBNull(2) ? null : row.GetInt32(2),
                !row.IsDBNull(3)),
            new Dictionary<string, object?> { ["table"] = tableName },
            cancellationToken);
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Orofoods.Web.Data.MigrationsPostgreSql;

namespace Orofoods.Web.Tests.Data;

public class PaymentTermsMigrationTests
{
    [Fact]
    public async Task Migration_maps_known_payment_terms_and_creates_missing_cash_and_credit_card()
    {
        await using var connection = await CreateLegacyPaymentTermsDatabaseAsync(
            ("PIX", true),
            ("À vista", true),
            ("Cartão de crédito", true),
            ("7 dias", true),
            ("14 dias", false));

        await ApplyPaymentTermsSqlAsync(connection);

        var terms = await ReadTermsAsync(connection);
        Assert.Equal("PIX", terms["PIX"].Code);
        Assert.Equal("CASH", terms["À vista"].Code);
        Assert.Equal("CREDIT_CARD", terms["Cartão de crédito"].Code);
        Assert.Equal("BOLETO_7D", terms["7 dias"].Code);
        Assert.Equal(7, terms["7 dias"].DaysUntilDue);
        Assert.Equal("BOLETO_14D", terms["14 dias"].Code);
        Assert.Equal(14, terms["14 dias"].DaysUntilDue);
        Assert.False(terms["14 dias"].IsActive);
        Assert.Single(terms.Values, term => term.Code == "CASH");
        Assert.Single(terms.Values, term => term.Code == "CREDIT_CARD");
    }

    [Fact]
    public async Task Migration_preserves_active_unknown_payment_terms_without_assigning_commercial_code()
    {
        await using var connection = await CreateLegacyPaymentTermsDatabaseAsync(
            ("Prazo ERP WMC", true),
            ("Condição personalizada", true));

        await ApplyPaymentTermsSqlAsync(connection);

        var terms = await ReadTermsAsync(connection);
        foreach (var name in new[] { "Prazo ERP WMC", "Condição personalizada" })
        {
            Assert.True(terms[name].IsActive);
            Assert.Equal(string.Empty, terms[name].Code);
            Assert.Equal(0, terms[name].DaysUntilDue);
        }
    }

    [Fact]
    public async Task Migration_preserves_inactive_unknown_payment_terms()
    {
        await using var connection = await CreateLegacyPaymentTermsDatabaseAsync(("Condição antiga", false));

        await ApplyPaymentTermsSqlAsync(connection);

        var term = (await ReadTermsAsync(connection))["Condição antiga"];
        Assert.False(term.IsActive);
        Assert.Equal(string.Empty, term.Code);
        Assert.Equal(0, term.DaysUntilDue);
    }

    private static async Task<SqliteConnection> CreateLegacyPaymentTermsDatabaseAsync(params (string Name, bool IsActive)[] terms)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE "PaymentTerms" (
                "Id" INTEGER PRIMARY KEY,
                "Name" TEXT NOT NULL,
                "SortOrder" INTEGER NOT NULL,
                "IsActive" BOOLEAN NOT NULL,
                "Code" TEXT NOT NULL DEFAULT '',
                "DaysUntilDue" INTEGER NOT NULL DEFAULT 0
            );
            """;
        await command.ExecuteNonQueryAsync();

        foreach (var (name, isActive) in terms)
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO \"PaymentTerms\" (\"Name\", \"SortOrder\", \"IsActive\") VALUES ($name, 1, $active)";
            insert.Parameters.AddWithValue("$name", name);
            insert.Parameters.AddWithValue("$active", isActive);
            await insert.ExecuteNonQueryAsync();
        }

        return connection;
    }

    private static async Task ApplyPaymentTermsSqlAsync(SqliteConnection connection)
    {
        var migration = new AddPaymentEligibilityAndReceivables();
        var commands = migration.UpOperations
            .OfType<SqlOperation>()
            .Where(operation => operation.Sql.Contains("\"PaymentTerms\"", StringComparison.Ordinal));

        foreach (var operation in commands)
        {
            foreach (var statement in operation.Sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = statement;
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private static async Task<Dictionary<string, (string Code, int DaysUntilDue, bool IsActive)>> ReadTermsAsync(SqliteConnection connection)
    {
        var terms = new Dictionary<string, (string Code, int DaysUntilDue, bool IsActive)>();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Name\", \"Code\", \"DaysUntilDue\", \"IsActive\" FROM \"PaymentTerms\"";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            terms.Add(reader.GetString(0), (reader.GetString(1), reader.GetInt32(2), reader.GetBoolean(3)));
        }

        return terms;
    }
}

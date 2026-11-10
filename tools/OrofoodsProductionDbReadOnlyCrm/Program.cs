using System.Data;
using Npgsql;
using OrofoodsProductionDbReadOnly;

internal static class Program
{
    private const string ConnectionStringVariable = "ConnectionStrings__DefaultConnection";
    private const int ConnectionTimeoutSeconds = 10;
    private const int CommandTimeoutSeconds = 15;
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(45);

    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 0)
            return Fail("This diagnostic accepts no arguments.");

        if (!ProductionDiagnosticContract.IsProductionEnvironment(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")))
        {
            return Fail("ASPNETCORE_ENVIRONMENT must be Production.");
        }

        var rawConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(rawConnectionString))
            return Fail("The authorized connection-string environment variable is not set.");

        string[] projectMigrationIds;
        try
        {
            projectMigrationIds = ProductionDiagnosticContract.LoadMigrationManifest(
                Path.Combine(AppContext.BaseDirectory, "migrations.json"));
        }
        catch
        {
            return Fail("The packaged migration manifest is missing or invalid.");
        }

        NpgsqlConnectionStringBuilder connectionString;
        try
        {
            connectionString = new NpgsqlConnectionStringBuilder(rawConnectionString);
        }
        catch
        {
            return Fail("The authorized connection string is invalid; its value was not displayed.");
        }

        if (!ProductionDiagnosticContract.IsExpectedHost(connectionString.Host))
            return Fail("Connection target rejected; the Production PostgreSQL host did not match.");

        connectionString.SslMode = SslMode.VerifyFull;
        connectionString.Timeout = ConnectionTimeoutSeconds;
        connectionString.CommandTimeout = CommandTimeoutSeconds;
        connectionString.Pooling = false;
        connectionString.IncludeErrorDetail = false;
        connectionString.ApplicationName = "OrofoodsProductionDbReadOnly";

        using var timeout = new CancellationTokenSource(OverallTimeout);

        try
        {
            await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
            await connection.OpenAsync(timeout.Token);

            var appliedMigrationIds = new HashSet<string>(StringComparer.Ordinal);
            long duplicatePhoneGroups;
            long affectedConversationRows;
            string databaseName;

            await using (var transaction = await connection.BeginTransactionAsync(
                             IsolationLevel.RepeatableRead,
                             timeout.Token))
            {
                try
                {
                    await using (var setReadOnly = CreateCommand(
                                     ProductionDiagnosticContract.ReadOnlyCommandText,
                                     connection,
                                     transaction))
                    {
                        await setReadOnly.ExecuteNonQueryAsync(timeout.Token);
                    }

                    await using (var transactionMode = CreateCommand(
                                     ProductionDiagnosticContract.TransactionModeCommandText,
                                     connection,
                                     transaction))
                    {
                        var value = await transactionMode.ExecuteScalarAsync(timeout.Token);
                        if (!string.Equals(Convert.ToString(value), "on", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("Read-only transaction was not confirmed.");
                    }

                    await using (var databaseCommand = CreateCommand(
                                     ProductionDiagnosticContract.DatabaseCommandText,
                                     connection,
                                     transaction))
                    {
                        databaseName = Convert.ToString(await databaseCommand.ExecuteScalarAsync(timeout.Token))
                                       ?? string.Empty;
                        if (!string.Equals(
                                databaseName,
                                ProductionDiagnosticContract.ExpectedDatabase,
                                StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Unexpected Production database.");
                        }
                    }

                    await using (var migrationCommand = CreateCommand(
                                     ProductionDiagnosticContract.MigrationHistoryCommandText,
                                     connection,
                                     transaction))
                    await using (var reader = await migrationCommand.ExecuteReaderAsync(timeout.Token))
                    {
                        while (await reader.ReadAsync(timeout.Token))
                        {
                            var migrationId = reader.GetString(0);
                            if (!ProductionDiagnosticContract.IsMigrationId(migrationId)
                                || !appliedMigrationIds.Add(migrationId))
                            {
                                throw new InvalidOperationException("Migration history contained an invalid row.");
                            }
                        }
                    }

                    await using (var duplicateCommand = CreateCommand(
                                     ProductionDiagnosticContract.DuplicatePhoneAggregateCommandText,
                                     connection,
                                     transaction))
                    await using (var reader = await duplicateCommand.ExecuteReaderAsync(timeout.Token))
                    {
                        if (!await reader.ReadAsync(timeout.Token))
                            throw new InvalidOperationException("Duplicate aggregate query returned no row.");

                        duplicatePhoneGroups = reader.GetInt64(0);
                        affectedConversationRows = reader.GetInt64(1);
                    }

                    await transaction.RollbackAsync(timeout.Token);
                }
                catch
                {
                    using var rollbackTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        await transaction.RollbackAsync(rollbackTimeout.Token);
                    }
                    catch
                    {
                        // Closing the connection also abandons the read-only transaction.
                    }

                    throw;
                }
            }

            Console.WriteLine($"Environment: {ProductionDiagnosticContract.ExpectedEnvironment}");
            Console.WriteLine($"PostgreSQL host: {ProductionDiagnosticContract.ExpectedHost}");
            Console.WriteLine($"Database: {databaseName}");
            Console.WriteLine("Transaction: READ ONLY confirmed; rolled back.");
            Console.WriteLine($"Duplicate phone groups: {duplicatePhoneGroups}");
            Console.WriteLine($"Affected conversation rows: {affectedConversationRows}");
            Console.WriteLine("Migration statuses:");
            foreach (var status in ProductionDiagnosticContract.FormatMigrationStatuses(
                         projectMigrationIds,
                         appliedMigrationIds))
            {
                Console.WriteLine($"  {status}");
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            return Fail("The read-only PostgreSQL diagnostic timed out.");
        }
        catch
        {
            return Fail("The read-only PostgreSQL diagnostic failed; no connection details were displayed.");
        }
    }

    private static NpgsqlCommand CreateCommand(
        string commandText,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction) =>
        new(commandText, connection, transaction)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 2;
    }
}

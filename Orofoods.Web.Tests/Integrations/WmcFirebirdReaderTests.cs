using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Integrations;

public sealed class WmcFirebirdReaderTests
{
    private const string BrandListSql = WmcBrandReader.BrandListSql;
    private const string SimilarButDifferentBrandSql = "SELECT m.CODMARCA, stats.ACTIVE_PRODUCT_COUNT FROM MARCAS m JOIN (SELECT p.CODMARCA, COUNT(*) AS ACTIVE_PRODUCT_COUNT FROM PRODUTOS p GROUP BY p.CODMARCA) stats ON stats.CODMARCA = m.CODMARCA";
    private const string OtherReadSql = "SELECT CODCLIENTE, NOME FROM CLIENTES";

    [Fact]
    public async Task BrandList_executes_without_explicit_transaction_and_logs_implicit_mode()
    {
        var connection = new FakeConnection();
        var logger = new RecordingLogger<WmcFirebirdReader>();
        logger.StateAtLog = () => connection.LastCommand?.LastReader?.IsClosed == true;
        var reader = CreateReader(connection, logger);

        var result = await reader.QueryAsync(BrandListSql, row => row.GetString(1));

        Assert.Equal(["Visconti"], result);
        Assert.Equal(0, connection.BeginTransactionCalls);
        Assert.NotNull(connection.LastCommand);
        Assert.Null(connection.LastCommand!.Transaction);
        Assert.Contains("TransactionMode=Implicit", Assert.Single(logger.Messages));
        Assert.True(Assert.Single(logger.ReaderClosedAtLog));
        Assert.True(connection.LastCommand.IsDisposed);
        Assert.True(connection.LastCommand.LastReader!.IsClosed);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task Similar_brand_query_keeps_explicit_snapshot_transaction()
    {
        var connection = new FakeConnection();
        var reader = CreateReader(connection);

        await reader.QueryAsync(SimilarButDifferentBrandSql, row => row.GetString(1));

        Assert.Equal(1, connection.BeginTransactionCalls);
        Assert.Equal(IsolationLevel.Snapshot, connection.LastIsolationLevel);
        Assert.Same(connection.Transaction, connection.LastCommand!.Transaction);
        Assert.Equal(1, connection.Transaction.CommitCalls);
    }

    [Fact]
    public async Task Other_read_keeps_snapshot_transaction_and_commits_it()
    {
        var connection = new FakeConnection();
        var reader = CreateReader(connection);

        var result = await reader.QueryAsync(OtherReadSql, row => row.GetString(1));

        Assert.Equal(["Visconti"], result);
        Assert.Equal(1, connection.BeginTransactionCalls);
        Assert.Equal(IsolationLevel.Snapshot, connection.LastIsolationLevel);
        Assert.Same(connection.Transaction, connection.LastCommand!.Transaction);
        Assert.Equal(1, connection.Transaction.CommitCalls);
        Assert.True(connection.Transaction.IsDisposed);
        Assert.True(connection.LastCommand.LastReader!.IsClosed);
    }

    [Fact]
    public async Task Explicit_transaction_timeout_propagates_cancellation_without_executing_sql()
    {
        var connection = new FakeConnection { DelayTransactionOpening = true };
        var reader = CreateReader(connection);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.QueryAsync(OtherReadSql, _ => 0, cancellationToken: timeout.Token));

        Assert.Equal(1, connection.BeginTransactionCalls);
        Assert.Equal(0, connection.CreateCommandCalls);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task BrandList_command_cancellation_propagates_without_explicit_transaction()
    {
        var connection = new FakeConnection { DelayCommandExecution = true };
        var reader = CreateReader(connection);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(25));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.QueryAsync(BrandListSql, _ => 0, cancellationToken: cancellation.Token));

        Assert.Equal(0, connection.BeginTransactionCalls);
        Assert.True(connection.LastCommand!.IsDisposed);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task Explicit_transaction_provider_failure_is_rethrown_and_resources_are_released()
    {
        var connection = new FakeConnection { TransactionOpeningException = new InvalidOperationException("provider failure") };
        var reader = CreateReader(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.QueryAsync(OtherReadSql, _ => 0));

        Assert.Equal(1, connection.BeginTransactionCalls);
        Assert.Equal(0, connection.CreateCommandCalls);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task Provider_failure_during_command_disposes_explicit_transaction_and_connection()
    {
        var connection = new FakeConnection { CommandExecutionException = new InvalidOperationException("command failure") };
        var reader = CreateReader(connection);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.QueryAsync(OtherReadSql, _ => 0));

        Assert.True(connection.LastCommand!.IsDisposed);
        Assert.True(connection.Transaction.IsDisposed);
        Assert.True(connection.IsDisposed);
    }

    [Fact]
    public async Task BrandList_reader_close_failure_is_logged_as_result_close_and_releases_resources()
    {
        var connection = new FakeConnection { ReaderDisposeException = new InvalidOperationException("close failure") };
        var logger = new RecordingLogger<WmcFirebirdReader>();
        var reader = CreateReader(connection, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.QueryAsync(BrandListSql, row => row.GetString(1)));

        Assert.Contains("FailurePhase=ResultClose", Assert.Single(logger.Messages));
        Assert.True(connection.LastCommand!.IsDisposed);
        Assert.True(connection.IsDisposed);
    }

    private static WmcFirebirdReader CreateReader(FakeConnection connection, RecordingLogger<WmcFirebirdReader>? logger = null) =>
        new(
            new FakeConnectionFactory(connection),
            Options.Create(new WmcFirebirdOptions { CommandTimeoutSeconds = 30 }),
            logger ?? new RecordingLogger<WmcFirebirdReader>());

    private sealed class FakeConnectionFactory(FakeConnection connection) : IWmcConnectionFactory
    {
        public Task<DbConnection> OpenAsync(CancellationToken cancellationToken = default)
        {
            connection.OpenToken = cancellationToken;
            connection.OpenCalls++;
            connection.Open();
            return Task.FromResult<DbConnection>(connection);
        }
    }

    private sealed class FakeConnection : DbConnection
    {
        private readonly FakeTransaction transaction;
        private ConnectionState state = ConnectionState.Closed;

        public FakeConnection()
        {
            transaction = new FakeTransaction();
        }

        public int OpenCalls { get; set; }
        public CancellationToken OpenToken { get; set; }
        public int BeginTransactionCalls { get; private set; }
        public int CreateCommandCalls { get; private set; }
        public IsolationLevel? LastIsolationLevel { get; private set; }
        public bool DelayTransactionOpening { get; init; }
        public Exception? TransactionOpeningException { get; init; }
        public bool DelayCommandExecution { get; init; }
        public Exception? CommandExecutionException { get; init; }
        public Exception? ReaderDisposeException { get; init; }
        public FakeCommand? LastCommand { get; private set; }
        public FakeTransaction Transaction => transaction;
        public bool IsDisposed { get; private set; }
        public DbDataReader? LastReader { get; private set; }

        [AllowNull]
        public override string ConnectionString { get; set; } = "fake";
        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "fake";
        public override ConnectionState State => state;

        public override void Open() => state = ConnectionState.Open;

        public override void Close() => state = ConnectionState.Closed;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            OpenToken = cancellationToken;
            Open();
            return Task.CompletedTask;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            BeginTransactionCalls++;
            LastIsolationLevel = isolationLevel;
            ThrowIfTransactionOpeningFails();
            return transaction;
        }

        protected override async ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken = default)
        {
            BeginTransactionCalls++;
            LastIsolationLevel = isolationLevel;
            if (DelayTransactionOpening)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            ThrowIfTransactionOpeningFails();
            return transaction;
        }

        protected override DbCommand CreateDbCommand()
        {
            CreateCommandCalls++;
            LastCommand = new FakeCommand(this)
            {
                DelayCommandExecution = DelayCommandExecution,
                CommandExecutionException = CommandExecutionException,
                ReaderDisposeException = ReaderDisposeException
            };
            return LastCommand;
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            state = ConnectionState.Closed;
            base.Dispose(disposing);
        }

        private void ThrowIfTransactionOpeningFails()
        {
            if (TransactionOpeningException is not null)
            {
                throw TransactionOpeningException;
            }
        }
    }

    private sealed class FakeCommand(FakeConnection connection) : DbCommand
    {
        private readonly FakeParameterCollection parameters = new();

        public bool IsDisposed { get; private set; }
        public DbDataReader? LastReader { get; private set; }
        public bool DelayCommandExecution { get; init; }
        public Exception? CommandExecutionException { get; init; }
        public Exception? ReaderDisposeException { get; init; }

        [AllowNull]
        public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; } = CommandType.Text;
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        [AllowNull]
        protected override DbConnection DbConnection { get; set; } = connection;
        protected override DbParameterCollection DbParameterCollection => parameters;
        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            if (CommandExecutionException is not null) throw CommandExecutionException;
            return LastReader = CreateReader();
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            if (DelayCommandExecution)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            if (CommandExecutionException is not null) throw CommandExecutionException;
            return LastReader = CreateReader();
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }

        private DbDataReader CreateReader()
        {
            var table = new DataTable();
            table.Columns.Add("CODIGO", typeof(int));
            table.Columns.Add("DESCRICAO", typeof(string));
            table.Rows.Add(5, "Visconti");
            return new TrackingDataReader(table.CreateDataReader(), ReaderDisposeException);
        }
    }

    private sealed class TrackingDataReader(DbDataReader inner, Exception? disposeException) : DbDataReader
    {
        public override object this[int ordinal] => inner[ordinal];
        public override object this[string name] => inner[name];
        public override int Depth => inner.Depth;
        public override int FieldCount => inner.FieldCount;
        public override bool HasRows => inner.HasRows;
        public override bool IsClosed => inner.IsClosed;
        public override int RecordsAffected => inner.RecordsAffected;
        public override int VisibleFieldCount => inner.VisibleFieldCount;

        public override void Close() => inner.Close();
        public override bool NextResult() => inner.NextResult();
        public override bool Read() => inner.Read();
        public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
        public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        public override char GetChar(int ordinal) => inner.GetChar(ordinal);
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
        public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
        public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
        public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
        public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
        public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
        public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
        public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
        public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
        public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
        public override string GetName(int ordinal) => inner.GetName(ordinal);
        public override int GetOrdinal(string name) => inner.GetOrdinal(name);
        public override string GetString(int ordinal) => inner.GetString(ordinal);
        public override object GetValue(int ordinal) => inner.GetValue(ordinal);
        public override int GetValues(object[] values) => inner.GetValues(values);
        public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
        public override IEnumerator GetEnumerator() => inner.GetEnumerator();
        public override DataTable? GetSchemaTable() => inner.GetSchemaTable();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
                if (disposeException is not null) throw disposeException;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            Dispose(true);
            await base.DisposeAsync();
        }
    }

    private sealed class FakeTransaction : DbTransaction
    {
        public int CommitCalls { get; private set; }
        public bool IsDisposed { get; private set; }
        public override IsolationLevel IsolationLevel => IsolationLevel.Snapshot;
        protected override DbConnection? DbConnection => null;

        public override void Commit() => CommitCalls++;
        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            return Task.CompletedTask;
        }

        public override void Rollback() { }
        public override Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class FakeParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }
        [AllowNull]
        public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        [AllowNull]
        public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class FakeParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> values = [];

        public override int Count => values.Count;
        public override object SyncRoot => ((ICollection)values).SyncRoot!;
        public override int Add(object value)
        {
            values.Add((DbParameter)value);
            return values.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values) Add(value!);
        }

        public override void Clear() => values.Clear();
        public override bool Contains(object value) => values.Contains((DbParameter)value);
        public override bool Contains(string value) => values.Any(parameter => parameter.ParameterName == value);
        public override void CopyTo(Array array, int index) => ((ICollection)values).CopyTo(array, index);
        public override IEnumerator GetEnumerator() => values.GetEnumerator();
        public override int IndexOf(object value) => values.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => values.FindIndex(parameter => parameter.ParameterName == parameterName);
        public override void Insert(int index, object value) => values.Insert(index, (DbParameter)value);
        public override void Remove(object value) => values.Remove((DbParameter)value);
        public override void RemoveAt(int index) => values.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => values[index];
        protected override DbParameter GetParameter(string parameterName) => values[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => values[IndexOf(parameterName)] = value;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        public List<bool> ReaderClosedAtLog { get; } = [];
        public Func<bool>? StateAtLog { get; set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
            if (StateAtLog is not null) ReaderClosedAtLog.Add(StateAtLog());
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }
}

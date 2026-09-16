using Microsoft.Extensions.Logging;

namespace Orofoods.Web.Infrastructure.Logging;

public sealed class DailyFileLoggerProvider(
    string directoryPath,
    LogLevel minimumLevel,
    TimeProvider? timeProvider = null) : ILoggerProvider, ISupportExternalScope
{
    private readonly object _writeLock = new();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public ILogger CreateLogger(string categoryName) => new DailyFileLogger(this, categoryName);

    public void Dispose()
    {
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel && logLevel != LogLevel.None;

    private void Write<TState>(
        string categoryName,
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var timestamp = _timeProvider.GetUtcNow();
        var filePath = Path.Combine(directoryPath, $"orofoods-{timestamp:yyyy-MM-dd}.log");
        var message = formatter(state, exception);
        var eventText = eventId.Id == 0 && eventId.Name is null ? string.Empty : $" ({eventId})";
        var scopeText = GetScopeText();
        var exceptionText = exception is null ? string.Empty : $"{Environment.NewLine}{exception}";
        var entry = $"{timestamp:O} [{logLevel}] {categoryName}{eventText}{scopeText}: {message}{exceptionText}{Environment.NewLine}";

        lock (_writeLock)
        {
            Directory.CreateDirectory(directoryPath);
            File.AppendAllText(filePath, entry);
        }
    }

    private string GetScopeText()
    {
        var scopes = new List<string>();
        _scopeProvider.ForEachScope(
            static (scope, state) =>
            {
                var formattedScope = FormatScope(scope);
                if (!string.IsNullOrEmpty(formattedScope))
                {
                    state.Add(formattedScope);
                }
            },
            scopes);

        return scopes.Count == 0 ? string.Empty : $" [{string.Join("; ", scopes)}]";
    }

    private static string FormatScope(object? scope)
    {
        if (scope is IEnumerable<KeyValuePair<string, object>> values)
        {
            return string.Join(", ", values
                .Where(pair => string.Equals(pair.Key, "CorrelationId", StringComparison.Ordinal))
                .Select(pair => $"{pair.Key}={pair.Value}"));
        }

        return string.Empty;
    }

    private sealed class DailyFileLogger(DailyFileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => provider.Write(categoryName, logLevel, eventId, state, exception, formatter);
    }
}

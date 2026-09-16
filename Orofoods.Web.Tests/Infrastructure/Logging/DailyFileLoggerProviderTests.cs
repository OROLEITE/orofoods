using Microsoft.Extensions.Logging;
using Orofoods.Web.Infrastructure.Logging;

namespace Orofoods.Web.Tests.Infrastructure.Logging;

public sealed class DailyFileLoggerProviderTests
{
    [Fact]
    public void LogInformation_writes_a_timestamped_entry_to_the_daily_file()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-log-tests-{Guid.NewGuid():N}");
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 8, 30, 14, 15, 16, TimeSpan.Zero));

        try
        {
            using var provider = new DailyFileLoggerProvider(directory, LogLevel.Information, timeProvider);
            using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(provider));

            loggerFactory.CreateLogger("Pedidos").LogInformation("Pedido {OrderNumber} confirmado", "ORO-2026-001245");

            var file = Path.Combine(directory, "orofoods-2026-08-30.log");
            var entry = File.ReadAllText(file);

            Assert.Contains("2026-08-30T14:15:16.0000000+00:00", entry);
            Assert.Contains("[Information] Pedidos: Pedido ORO-2026-001245 confirmado", entry);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LogInformation_writes_the_active_logging_scope()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-log-tests-{Guid.NewGuid():N}");

        try
        {
            using var provider = new DailyFileLoggerProvider(directory, LogLevel.Information);
            using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(provider));
            var logger = loggerFactory.CreateLogger("Pedidos");

            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "e72342b5-8f4f-4daa-a096-8172e89e9968" }))
            {
                logger.LogInformation("Pedido confirmado");
            }

            var file = Directory.GetFiles(directory, "orofoods-*.log").Single();
            var entry = File.ReadAllText(file);

            Assert.Contains("CorrelationId=e72342b5-8f4f-4daa-a096-8172e89e9968", entry);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void LogInformation_does_not_persist_unapproved_scope_properties()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"orofoods-log-tests-{Guid.NewGuid():N}");

        try
        {
            using var provider = new DailyFileLoggerProvider(directory, LogLevel.Information);
            using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(provider));
            var logger = loggerFactory.CreateLogger("Pedidos");

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = "e72342b5-8f4f-4daa-a096-8172e89e9968",
                ["CustomerEmail"] = "compras@cliente.com"
            }))
            {
                logger.LogInformation("Pedido confirmado");
            }

            var file = Directory.GetFiles(directory, "orofoods-*.log").Single();
            var entry = File.ReadAllText(file);

            Assert.Contains("CorrelationId=e72342b5-8f4f-4daa-a096-8172e89e9968", entry);
            Assert.DoesNotContain("compras@cliente.com", entry);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

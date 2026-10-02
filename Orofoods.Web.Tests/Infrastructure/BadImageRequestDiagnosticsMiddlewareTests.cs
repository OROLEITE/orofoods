using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Infrastructure;
using Orofoods.Web.Infrastructure.Logging;
using Orofoods.Web.Integrations.Erp.Wmc;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class BadImageRequestDiagnosticsMiddlewareTests
{
    [Fact]
    public async Task Bad_image_request_logs_method_path_trace_and_endpoint_without_query_string()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var context = CreateContext();
        context.Request.Method = "POST";
        context.Request.Path = "/Admin/WhatsApp";
        context.Request.QueryString = new QueryString("?token=secret-query");
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(), "WhatsApp.Index"));
        using var activity = new Activity("bad-image-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var middleware = new BadImageRequestDiagnosticsMiddleware(_ =>
            Task.FromException(new BadImageFormatException("Bad IL range.")));

        await Assert.ThrowsAsync<BadImageFormatException>(() => middleware.InvokeAsync(context, logger));

        var log = Assert.Single(logger.Messages);
        Assert.Contains("HttpMethod=POST", log);
        Assert.Contains("RequestPath=/Admin/WhatsApp", log);
        Assert.Contains("TraceIdentifier=test-trace-id", log);
        Assert.Contains($"ActivityTraceId={activity.TraceId}", log);
        Assert.Contains($"ActivitySpanId={activity.SpanId}", log);
        Assert.Contains("Endpoint=WhatsApp.Index", log);
        Assert.DoesNotContain("secret-query", log);
    }

    [Fact]
    public async Task Bad_image_request_does_not_log_sensitive_headers()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var context = CreateContext();
        context.Request.Headers.Authorization = "Bearer secret-token";
        context.Request.Headers.Cookie = "session=secret-cookie";
        var middleware = ThrowingMiddleware();

        await Assert.ThrowsAsync<BadImageFormatException>(() => middleware.InvokeAsync(context, logger));

        var log = Assert.Single(logger.Messages);
        Assert.DoesNotContain("secret-token", log);
        Assert.DoesNotContain("secret-cookie", log);
        Assert.DoesNotContain("Authorization", log);
        Assert.DoesNotContain("Cookie", log);
    }

    [Fact]
    public async Task Bad_image_request_does_not_log_query_string()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?access_token=query-secret");

        await Assert.ThrowsAsync<BadImageFormatException>(() => ThrowingMiddleware().InvokeAsync(context, logger));

        Assert.DoesNotContain("query-secret", Assert.Single(logger.Messages));
    }

    [Fact]
    public async Task Bad_image_request_does_not_log_request_body()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var context = CreateContext();
        context.Request.Body = new MemoryStream("whatsapp message secret-body"u8.ToArray());
        var middleware = ThrowingMiddleware();

        await Assert.ThrowsAsync<BadImageFormatException>(() => middleware.InvokeAsync(context, logger));

        Assert.DoesNotContain("secret-body", Assert.Single(logger.Messages));
    }

    [Fact]
    public void Wmc_diagnostic_does_not_log_option_values()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var options = new WmcFileDropOptions
        {
            Enabled = true,
            AutoRetryEnabled = true,
            OutputDirectory = "private-wmc-directory"
        };
        var wrappedFailure = new TargetInvocationException(new BadImageFormatException("Bad IL range."));

        BadImageRuntimeDiagnostics.LogWmcOptionsFailure(
            logger,
            wrappedFailure,
            typeof(WmcFileDropOptions));

        var log = Assert.Single(logger.Messages);
        Assert.DoesNotContain(options.OutputDirectory, log);
        Assert.DoesNotContain("Enabled=True", log);
        Assert.DoesNotContain("AutoRetryEnabled=True", log);
        Assert.Contains("ErpRetryBackgroundService", log);
        Assert.Contains("options resolution / options construction", log);
    }

    [Fact]
    public void Assembly_diagnostic_includes_name_version_location_and_load_context()
    {
        var assembly = BadImageRuntimeDiagnostics.Describe(typeof(WmcFileDropOptions).Assembly);

        Assert.Equal(typeof(WmcFileDropOptions).Assembly.GetName().Name, assembly.Name);
        Assert.Equal(typeof(WmcFileDropOptions).Assembly.GetName().Version?.ToString(), assembly.Version);
        Assert.Equal(typeof(WmcFileDropOptions).Assembly.Location, assembly.Location);
        Assert.NotNull(assembly.LoadContextName);
        Assert.True(assembly.IsDefaultContext);
    }

    [Fact]
    public void Assembly_diagnostic_reports_default_load_context_when_available()
    {
        var assembly = BadImageRuntimeDiagnostics.Describe(typeof(WmcFileDropOptions).Assembly);

        Assert.Equal(AssemblyLoadContext.Default.Name, assembly.LoadContextName);
        Assert.False(assembly.IsCollectible);
        Assert.True(assembly.IsDefaultContext);
    }

    [Fact]
    public async Task Bad_image_exception_is_rethrown_unchanged()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var context = CreateContext();
        var expected = new BadImageFormatException("Bad IL range.");
        var middleware = new BadImageRequestDiagnosticsMiddleware(_ => Task.FromException(expected));

        var actual = await Assert.ThrowsAsync<BadImageFormatException>(() => middleware.InvokeAsync(context, logger));

        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task Healthy_request_does_not_write_bad_image_log()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var middleware = new BadImageRequestDiagnosticsMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(CreateContext(), logger);

        Assert.Empty(logger.Messages);
    }

    [Fact]
    public async Task Ef_metadata_failure_logs_context_and_category_without_sql_or_parameters()
    {
        var logger = new RecordingLogger<BadImageRequestDiagnosticsMiddleware>();
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?sql=SELECT-secret&parameter=customer-secret");
        context.Request.Body = new MemoryStream("customer-secret"u8.ToArray());
        var middleware = new BadImageRequestDiagnosticsMiddleware(_ =>
        {
            Microsoft.EntityFrameworkCore.Metadata.FakeEfMetadataFailure.CreateModel();
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<BadImageFormatException>(() => middleware.InvokeAsync(context, logger));

        var log = Assert.Single(logger.Messages);
        Assert.Contains("DbContextType=Orofoods.Web.Data.PostgreSqlApplicationDbContext", log);
        Assert.Contains("OperationCategory=model-building", log);
        Assert.DoesNotContain("SELECT-secret", log);
        Assert.DoesNotContain("customer-secret", log);
    }

    private static DefaultHttpContext CreateContext() => new()
    {
        TraceIdentifier = "test-trace-id"
    };

    private static BadImageRequestDiagnosticsMiddleware ThrowingMiddleware() =>
        new(_ => Task.FromException(new BadImageFormatException("Bad IL range.")));

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add($"{eventId.Name}: {formatter(state, exception)}");
        }
    }
}

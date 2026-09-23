using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Orofoods.Web.Infrastructure.Diagnostics;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class MainDataProtectionBlobReadDiagnosticTests
{
    private const string BlobUri = "https://storofoodsstg01.blob.core.windows.net/data-protection/keys.xml";
    private const string ProbeKeySentinel = "TASK17_MAIN_PROBEKEY_SENTINEL_91D4";
    private const string TokenSentinel = "eyJhbGciOiJub25lIn0.eyJzdWIiOiJUQVNLMTdfVE9LRU5fU0VOVElORUwiLCJleHAiOjQxMDI0NDQ4MDB9.signature";

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", true)]
    [InlineData("Staging", false)]
    public async Task Endpoint_is_absent_outside_staging_with_the_existing_probe_flag_enabled(string environmentName, bool enabled)
    {
        await using var app = BuildApp(environmentName, enabled);

        MainDataProtectionBlobReadEndpoint.MapIfAvailable(app);

        Assert.DoesNotContain(
            Routes(app),
            route => route == MainDataProtectionBlobReadEndpoint.Route);
    }

    [Fact]
    public async Task Endpoint_is_registered_in_staging_when_the_existing_probe_flag_is_enabled()
    {
        await using var app = BuildApp(Environments.Staging, enabled: true);

        MainDataProtectionBlobReadEndpoint.MapIfAvailable(app);

        Assert.Contains(
            Routes(app),
            route => route == MainDataProtectionBlobReadEndpoint.Route);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(ProbeKeySentinel, null)]
    [InlineData(ProbeKeySentinel, "wrong-key")]
    public async Task Missing_or_incorrect_probe_key_returns_404_without_entering_the_handler(
        string? configuredKey,
        string? providedKey)
    {
        var logs = new CapturingLoggerProvider();
        var authorization = CreateAuthorizationProbe(configuredKey, logs);
        var diagnostic = new FakeDiagnostic(MainDataProtectionBlobReadResult.Success(200));
        var context = new DefaultHttpContext();
        if (providedKey is not null)
        {
            context.Request.Headers["X-Orofoods-Diagnostic-Key"] = providedKey;
        }

        var result = await MainDataProtectionBlobReadEndpoint.HandleAsync(
            context,
            authorization,
            diagnostic,
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, diagnostic.ExecuteCount);
        Assert.DoesNotContain(logs.Messages, message =>
            message.Contains(ProbeKeySentinel, StringComparison.Ordinal) ||
            message.Contains(TokenSentinel, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Correct_probe_key_enters_the_handler_and_returns_only_the_sanitized_contract()
    {
        var logs = new CapturingLoggerProvider();
        var authorization = CreateAuthorizationProbe(ProbeKeySentinel, logs);
        var diagnostic = new FakeDiagnostic(MainDataProtectionBlobReadResult.Success(200));
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Orofoods-Diagnostic-Key"] = ProbeKeySentinel;

        var result = await MainDataProtectionBlobReadEndpoint.HandleAsync(
            context,
            authorization,
            diagnostic,
            CancellationToken.None);

        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        var body = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        var json = JsonSerializer.Serialize(body);
        Assert.Equal(StatusCodes.Status200OK, status.StatusCode);
        Assert.Equal(1, diagnostic.ExecuteCount);
        Assert.Contains("\"target\":\"keys.xml\"", json, StringComparison.Ordinal);
        Assert.Contains("\"propertiesSucceeded\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"downloadSucceeded\":true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("content", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("etag", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProbeKeySentinel, json, StringComparison.Ordinal);
        Assert.DoesNotContain(TokenSentinel, json, StringComparison.Ordinal);
        Assert.DoesNotContain(logs.Messages, message =>
            message.Contains(ProbeKeySentinel, StringComparison.Ordinal) ||
            message.Contains(TokenSentinel, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Diagnostic_reads_the_exact_configured_main_blob_in_properties_then_download_order()
    {
        var client = new RecordingReadClient();
        var factory = new RecordingReadClientFactory(client);
        var diagnostic = CreateDiagnostic(factory);

        var result = await diagnostic.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["GetProperties", "DownloadStreaming"], client.Operations);
        Assert.Equal(new Uri(BlobUri), factory.BlobUri);
        Assert.IsType<DiagnosticTokenCredential>(factory.Credential);
        Assert.Equal(MainDataProtectionBlobReadResult.Success(200), result);
    }

    [Fact]
    public async Task Properties_failure_stops_before_download_and_returns_no_exception_message()
    {
        var client = new RecordingReadClient
        {
            PropertiesException = new RequestFailedException(
                403,
                $"authorization={TokenSentinel}; probe={ProbeKeySentinel}",
                "AuthorizationFailure",
                null)
        };
        var diagnostic = CreateDiagnostic(new RecordingReadClientFactory(client));

        var result = await diagnostic.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["GetProperties"], client.Operations);
        Assert.False(result.PropertiesSucceeded);
        Assert.False(result.DownloadSucceeded);
        Assert.Equal(403, result.HttpStatus);
        Assert.Equal("AuthorizationFailure", result.ErrorCode);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(ProbeKeySentinel, json, StringComparison.Ordinal);
        Assert.DoesNotContain(TokenSentinel, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_failure_preserves_properties_success_and_returns_only_the_azure_error_code()
    {
        var client = new RecordingReadClient
        {
            DownloadException = new RequestFailedException(
                403,
                $"access_token={TokenSentinel}; probe={ProbeKeySentinel}",
                "AuthorizationPermissionMismatch",
                null)
        };
        var diagnostic = CreateDiagnostic(new RecordingReadClientFactory(client));

        var result = await diagnostic.ExecuteAsync(CancellationToken.None);

        Assert.Equal(["GetProperties", "DownloadStreaming"], client.Operations);
        Assert.True(result.PropertiesSucceeded);
        Assert.False(result.DownloadSucceeded);
        Assert.Equal(403, result.HttpStatus);
        Assert.Equal("AuthorizationPermissionMismatch", result.ErrorCode);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(ProbeKeySentinel, json, StringComparison.Ordinal);
        Assert.DoesNotContain(TokenSentinel, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Azure_client_adapter_performs_only_properties_and_streaming_download_and_discards_content()
    {
        var rawResponse = new Mock<Response>();
        rawResponse.SetupGet(response => response.Status).Returns(200);
        var content = new MemoryStream([1, 2, 3, 4]);
        var blob = new Mock<BlobClient>(MockBehavior.Strict);
        blob.Setup(client => client.GetPropertiesAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue<BlobProperties>(null!, rawResponse.Object));
        blob.Setup(client => client.DownloadStreamingAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(
                BlobsModelFactory.BlobDownloadStreamingResult(content, null!),
                rawResponse.Object));
        var adapter = new MainDataProtectionBlobReadClient(blob.Object);

        var propertiesStatus = await adapter.GetPropertiesAsync(CancellationToken.None);
        var downloadStatus = await adapter.DownloadToNullAsync(CancellationToken.None);

        Assert.Equal(200, propertiesStatus);
        Assert.Equal(200, downloadStatus);
        Assert.False(content.CanRead);
        blob.VerifyAll();
        blob.VerifyNoOtherCalls();
    }

    private static MainDataProtectionBlobReadDiagnostic CreateDiagnostic(IMainDataProtectionBlobReadClientFactory factory)
    {
        var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(new CapturingLoggerProvider()));
        return new MainDataProtectionBlobReadDiagnostic(
            Configuration(
                ("Diagnostics:CredentialProbe", "true"),
                ("DataProtection:Azure:Enabled", "true"),
                ("DataProtection:Azure:BlobUri", BlobUri)),
            Environment(Environments.Staging),
            factory,
            loggerFactory);
    }

    private static IsolatedDataProtectionProbe CreateAuthorizationProbe(string? configuredKey, CapturingLoggerProvider logs)
    {
        var values = configuredKey is null
            ? Array.Empty<(string Key, string Value)>()
            : [("Diagnostics:ProbeKey", configuredKey)];
        var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        return new IsolatedDataProtectionProbe(
            Configuration(values),
            Environment(Environments.Staging),
            new NoOpProviderFactory(),
            loggerFactory,
            loggerFactory.CreateLogger<IsolatedDataProtectionProbe>());
    }

    private static WebApplication BuildApp(string environmentName, bool enabled)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ApplicationName = typeof(MainDataProtectionBlobReadDiagnosticTests).Assembly.GetName().Name
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Diagnostics:CredentialProbe"] = enabled.ToString()
        });
        builder.Services.AddSingleton(CreateAuthorizationProbe("route-test-key", new CapturingLoggerProvider()));
        builder.Services.AddSingleton<IMainDataProtectionBlobReadDiagnostic>(
            new FakeDiagnostic(MainDataProtectionBlobReadResult.Success(200)));
        return builder.Build();
    }

    private static IEnumerable<string?> Routes(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText);

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();

    private static IHostEnvironment Environment(string name) => new TestHostEnvironment { EnvironmentName = name };

    private sealed class FakeDiagnostic(MainDataProtectionBlobReadResult result) : IMainDataProtectionBlobReadDiagnostic
    {
        public int ExecuteCount { get; private set; }

        public Task<MainDataProtectionBlobReadResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecuteCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingReadClientFactory(RecordingReadClient client) : IMainDataProtectionBlobReadClientFactory
    {
        public Uri? BlobUri { get; private set; }
        public TokenCredential? Credential { get; private set; }

        public IMainDataProtectionBlobReadClient Create(Uri blobUri, TokenCredential credential)
        {
            BlobUri = blobUri;
            Credential = credential;
            return client;
        }
    }

    private sealed class RecordingReadClient : IMainDataProtectionBlobReadClient
    {
        public List<string> Operations { get; } = [];
        public RequestFailedException? PropertiesException { get; init; }
        public RequestFailedException? DownloadException { get; init; }

        public Task<int> GetPropertiesAsync(CancellationToken cancellationToken)
        {
            Operations.Add("GetProperties");
            return PropertiesException is null
                ? Task.FromResult(200)
                : Task.FromException<int>(PropertiesException);
        }

        public Task<int> DownloadToNullAsync(CancellationToken cancellationToken)
        {
            Operations.Add("DownloadStreaming");
            return DownloadException is null
                ? Task.FromResult(200)
                : Task.FromException<int>(DownloadException);
        }
    }

    private sealed class NoOpProviderFactory : IIsolatedDataProtectionProviderFactory
    {
        public IsolatedDataProtectionProviderLease Create(
            Uri blobUri,
            Uri keyVaultKeyIdentifier,
            TokenCredential credential) =>
            throw new InvalidOperationException("Provider creation is not expected during authorization tests.");
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> messages = [];
        public IReadOnlyList<string> Messages => messages;
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(List<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Add(formatter(state, exception));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

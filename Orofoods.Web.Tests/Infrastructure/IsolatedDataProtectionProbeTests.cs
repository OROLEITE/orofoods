using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Infrastructure.Diagnostics;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class IsolatedDataProtectionProbeTests
{
    [Fact]
    public void Endpoint_is_available_only_in_staging_with_credential_probe_enabled()
    {
        var enabled = Configuration(("Diagnostics:CredentialProbe", "true"));

        Assert.True(IsolatedDataProtectionProbeEndpoint.IsAvailable(Environment("Staging"), enabled));
        Assert.False(IsolatedDataProtectionProbeEndpoint.IsAvailable(Environment("Production"), enabled));
        Assert.False(IsolatedDataProtectionProbeEndpoint.IsAvailable(Environment("Development"), enabled));
        Assert.False(IsolatedDataProtectionProbeEndpoint.IsAvailable(Environment("Staging"), Configuration()));
    }

    [Fact]
    public void Probe_key_is_required_and_compared_without_accepting_an_incorrect_value()
    {
        var probe = CreateProbe(Configuration(("Diagnostics:ProbeKey", "expected-key")));

        Assert.False(probe.IsAuthorized(string.Empty));
        Assert.False(probe.IsAuthorized("incorrect-key"));
        Assert.True(probe.IsAuthorized("expected-key"));
    }

    [Fact]
    public void Probe_logs_when_the_configured_key_is_absent_without_leaking_the_header_value()
    {
        const string headerSentinel = "TASK17_HEADER_SENTINEL_4B7C";
        var logs = new CapturingLoggerProvider();
        var probe = CreateProbe(Configuration(), logs);

        Assert.False(probe.IsAuthorized(headerSentinel));

        Assert.Contains(logs.Messages, message => message.Contains("configured key absent"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(headerSentinel));
    }

    [Fact]
    public void Probe_logs_when_the_header_is_missing_without_leaking_the_configured_key()
    {
        const string probeKeySentinel = "TASK17_PROBEKEY_SENTINEL_9F3A";
        var logs = new CapturingLoggerProvider();
        var probe = CreateProbe(Configuration(("Diagnostics:ProbeKey", probeKeySentinel)), logs);

        Assert.False(probe.IsAuthorized(string.Empty));

        Assert.Contains(logs.Messages, message => message.Contains("missing header"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(probeKeySentinel));
    }

    [Fact]
    public void Probe_logs_when_the_header_is_invalid_without_leaking_any_key()
    {
        const string probeKeySentinel = "TASK17_PROBEKEY_SENTINEL_9F3A";
        const string headerSentinel = "TASK17_HEADER_SENTINEL_4B7C";
        var logs = new CapturingLoggerProvider();
        var probe = CreateProbe(Configuration(("Diagnostics:ProbeKey", probeKeySentinel)), logs);

        Assert.False(probe.IsAuthorized(headerSentinel));

        Assert.Contains(logs.Messages, message => message.Contains("invalid key"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(probeKeySentinel) || message.Contains(headerSentinel));
    }

    [Fact]
    public async Task Authorized_handler_logs_entry_without_returning_ciphertext()
    {
        var logs = new CapturingLoggerProvider();
        var factory = new FakeFactory();
        var probe = CreateProbe(Configuration(
            ("Diagnostics:CredentialProbe", "true"),
            ("Diagnostics:ProbeKey", "TASK17_PROBEKEY_SENTINEL_9F3A"),
            ("DataProtection:Azure:Enabled", "true"),
            ("DataProtection:Azure:BlobUri", "https://storofoodsstg01.blob.core.windows.net/data-protection/keys.xml"),
            ("DataProtection:Azure:KeyVaultKeyIdentifier", "https://kv-orofoods-stg-01.vault.azure.net/keys/dp-orofoods-stg/key-version")), logs, factory);
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Orofoods-Diagnostic-Key"] = "TASK17_PROBEKEY_SENTINEL_9F3A";

        _ = await IsolatedDataProtectionProbeEndpoint.HandleAsync(context, probe, CancellationToken.None);

        Assert.Contains(logs.Messages, message => message.Contains("handler entered"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("ciphertext-not-returned") || message.Contains("TASK17_PROBEKEY_SENTINEL_9F3A"));
    }

    [Fact]
    public void Route_registration_telemetry_describes_when_the_route_is_or_is_not_available()
    {
        var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Orofoods.Web", "Infrastructure", "Diagnostics", "IsolatedDataProtectionProbe.cs"));

        Assert.Contains("Diagnostic DP probe route registration evaluated", source);
        Assert.Contains("registered={Registered}", source);
    }

    [Fact]
    public void Diagnostic_blob_uri_uses_a_separate_blob_in_the_same_container()
    {
        var uri = IsolatedDataProtectionProbe.BuildDiagnosticBlobUri(
            "https://storofoodsstg01.blob.core.windows.net/data-protection/keys.xml");

        Assert.Equal(
            "https://storofoodsstg01.blob.core.windows.net/data-protection/diagnostics/task17-probe-keys.xml",
            uri.ToString());
        Assert.NotEqual("keys.xml", uri.Segments.Last().Trim('/'));
    }

    [Fact]
    public async Task Probe_uses_an_isolated_provider_and_executes_protect_without_returning_ciphertext()
    {
        var factory = new FakeFactory();
        var probe = CreateProbe(Configuration(
            ("Diagnostics:CredentialProbe", "true"),
            ("DataProtection:Azure:Enabled", "true"),
            ("DataProtection:Azure:BlobUri", "https://storofoodsstg01.blob.core.windows.net/data-protection/keys.xml"),
            ("DataProtection:Azure:KeyVaultKeyIdentifier", "https://kv-orofoods-stg-01.vault.azure.net/keys/dp-orofoods-stg/key-version")), factory: factory);

        var result = await probe.ExecuteAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(factory.Protector.ProtectCalled);
        Assert.Equal("Task17Diagnostic", factory.Purpose);
        Assert.Equal(IsolatedDataProtectionProbe.ApplicationName, factory.ApplicationName);
        Assert.EndsWith(IsolatedDataProtectionProbe.BlobName, factory.BlobUri!.AbsolutePath);
        Assert.NotNull(factory.Credential);
    }

    [Fact]
    public async Task Probe_does_not_construct_or_use_a_provider_when_the_route_is_unavailable()
    {
        var factory = new FakeFactory();
        var probe = CreateProbe(Configuration(("DataProtection:Azure:Enabled", "true")), factory: factory);

        var result = await probe.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(factory.CreateCalled);
    }

    private static IsolatedDataProtectionProbe CreateProbe(
        IConfiguration configuration,
        CapturingLoggerProvider? logs = null,
        FakeFactory? factory = null) =>
        CreateProbeCore(configuration, logs ?? new CapturingLoggerProvider(), factory ?? new FakeFactory());

    private static IsolatedDataProtectionProbe CreateProbeCore(
        IConfiguration configuration,
        CapturingLoggerProvider logs,
        FakeFactory factory)
    {
        var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        return new IsolatedDataProtectionProbe(
            configuration,
            Environment("Staging"),
            factory,
            loggerFactory,
            loggerFactory.CreateLogger<IsolatedDataProtectionProbe>());
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();

    private static IHostEnvironment Environment(string name) => new TestHostEnvironment { EnvironmentName = name };

    private sealed class FakeFactory : IIsolatedDataProtectionProviderFactory
    {
        public bool CreateCalled { get; private set; }
        public Uri? BlobUri { get; private set; }
        public TokenCredential? Credential { get; private set; }
        public string Purpose { get; private set; } = string.Empty;
        public string ApplicationName => IsolatedDataProtectionProbe.ApplicationName;
        public FakeProtector Protector { get; } = new();

        public IsolatedDataProtectionProviderLease Create(Uri blobUri, Uri keyVaultKeyIdentifier, TokenCredential credential)
        {
            CreateCalled = true;
            BlobUri = blobUri;
            Credential = credential;
            return new IsolatedDataProtectionProviderLease(new FakeProvider(Protector, purpose => Purpose = purpose), new NoOpDisposable());
        }
    }

    private sealed class FakeProvider(FakeProtector protector, Action<string> setPurpose) : IDataProtectionProvider
    {
        public IDataProtector CreateProtector(string purpose)
        {
            setPurpose(purpose);
            return protector;
        }
    }

    private sealed class FakeProtector : IDataProtector
    {
        public bool ProtectCalled { get; private set; }
        public IDataProtector CreateProtector(string purpose) => this;
        public byte[] Protect(byte[] plaintext)
        {
            ProtectCalled = true;
            return plaintext;
        }
        public byte[] Unprotect(byte[] protectedData) => protectedData;
        public string Protect(string plaintext)
        {
            ProtectCalled = true;
            return "ciphertext-not-returned";
        }

        public string Unprotect(string protectedData) => protectedData;
    }

    private sealed class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
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
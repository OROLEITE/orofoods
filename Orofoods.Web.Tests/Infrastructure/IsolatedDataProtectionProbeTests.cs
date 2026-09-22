using Azure.Core;
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
            ("DataProtection:Azure:KeyVaultKeyIdentifier", "https://kv-orofoods-stg-01.vault.azure.net/keys/dp-orofoods-stg/key-version")), factory);

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
        var probe = CreateProbe(Configuration(("DataProtection:Azure:Enabled", "true")), factory);

        var result = await probe.ExecuteAsync(CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(factory.CreateCalled);
    }

    private static IsolatedDataProtectionProbe CreateProbe(
        IConfiguration configuration,
        FakeFactory? factory = null) =>
        new(
            configuration,
            Environment("Staging"),
            factory ?? new FakeFactory(),
            LoggerFactory.Create(_ => { }),
            LoggerFactory.Create(_ => { }).CreateLogger<IsolatedDataProtectionProbe>());

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

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
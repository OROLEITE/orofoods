using System.Collections.Concurrent;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orofoods.Web.Infrastructure.Diagnostics;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class AzureIdentityDiagnosticsTests
{
    [Fact]
    public void Credential_probe_is_disabled_by_default()
    {
        Assert.False(AzureIdentityDiagnostics.IsCredentialProbeEnabled(Environment("Staging"), Configuration()));
    }

    [Fact]
    public void Credential_probe_is_disabled_outside_staging_even_when_flag_is_enabled()
    {
        var configuration = Configuration(("Diagnostics:CredentialProbe", "true"));

        Assert.False(AzureIdentityDiagnostics.IsCredentialProbeEnabled(Environment("Development"), configuration));
        Assert.False(AzureIdentityDiagnostics.IsCredentialProbeEnabled(Environment("Production"), configuration));
    }

    [Fact]
    public void Credential_probe_requires_staging_and_the_explicit_flag()
    {
        var configuration = Configuration(("Diagnostics:CredentialProbe", "true"));

        Assert.True(AzureIdentityDiagnostics.IsCredentialProbeEnabled(Environment("Staging"), configuration));
    }

    [Fact]
    public void WrapDataProtectionCredential_returns_the_original_credential_when_probe_is_off_or_not_staging()
    {
        var offInner = new DefaultAzureCredential();
        var productionInner = new DefaultAzureCredential();
        var off = AzureIdentityDiagnostics.WrapDataProtectionCredential(offInner, Environment("Staging"), Configuration());
        var production = AzureIdentityDiagnostics.WrapDataProtectionCredential(
            productionInner,
            Environment("Production"),
            Configuration(("Diagnostics:CredentialProbe", "true")));

        Assert.Same(offInner, off);
        Assert.Same(productionInner, production);
    }

    [Fact]
    public void DiagnosticTokenCredential_delegates_context_and_returns_the_inner_access_token()
    {
        var expected = new AccessToken(CreateToken("oid-123", "tid-456"), DateTimeOffset.UtcNow.AddHours(1));
        var inner = new FakeTokenCredential(expected);
        var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        var credential = new DiagnosticTokenCredential(inner, loggerFactory.CreateLogger<DiagnosticTokenCredential>());
        var context = new TokenRequestContext(["https://storage.azure.com/.default"]);
        using var cancellationSource = new CancellationTokenSource();

        var actual = credential.GetToken(context, cancellationSource.Token);

        Assert.Equal(expected, actual);
        Assert.NotNull(inner.RequestContext);
        Assert.Equal(context.Scopes, inner.RequestContext.Value.Scopes);
        Assert.Equal(cancellationSource.Token, inner.CancellationToken);
        Assert.Contains(logs.Messages, message => message.Contains("oid-123") && message.Contains("tid-456"));
        Assert.DoesNotContain(logs.Messages, message => message.Contains(expected.Token));
    }

    [Fact]
    public async Task DiagnosticTokenCredential_delegates_async_access_token_without_logging_the_token()
    {
        var expected = new AccessToken(CreateToken("oid-async", "tid-async"), DateTimeOffset.UtcNow.AddHours(1));
        var inner = new FakeTokenCredential(expected);
        var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        var credential = new DiagnosticTokenCredential(inner, loggerFactory.CreateLogger<DiagnosticTokenCredential>());

        var actual = await credential.GetTokenAsync(new TokenRequestContext(["scope-a"]), CancellationToken.None);

        Assert.Equal(expected, actual);
        Assert.DoesNotContain(logs.Messages, message => message.Contains(expected.Token));
    }

    [Fact]
    public void DiagnosticTokenCredential_propagates_and_sanitizes_inner_failures()
    {
        const string token = "eyJhbGciOiJIUzI1NiJ9.eyJvaWQiOiIxMjMifQ.signature";
        var inner = new FakeTokenCredential(new InvalidOperationException($"Authorization: Bearer {token}; client_secret=secret-value"));
        var logs = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(logging => logging.AddProvider(logs));
        var credential = new DiagnosticTokenCredential(inner, loggerFactory.CreateLogger<DiagnosticTokenCredential>());

        Assert.Throws<InvalidOperationException>(() => credential.GetToken(new TokenRequestContext(["scope-a"]), CancellationToken.None));

        Assert.DoesNotContain(logs.Messages, message => message.Contains(token));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("secret-value"));
        Assert.Contains(logs.Messages, message => message.Contains(nameof(InvalidOperationException)));
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => new KeyValuePair<string, string?>(value.Key, value.Value))).Build();

    private static IHostEnvironment Environment(string environmentName) => new TestHostEnvironment { EnvironmentName = environmentName };

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Orofoods.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string CreateToken(string oid, string tid)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"oid":"{{oid}}","tid":"{{tid}}","appid":"app-id"}"""))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return $"header.{payload}.signature";
    }

    private sealed class FakeTokenCredential : TokenCredential
    {
        private readonly AccessToken? accessToken;
        private readonly Exception? exception;

        public FakeTokenCredential(AccessToken accessToken) => this.accessToken = accessToken;
        public FakeTokenCredential(Exception exception) => this.exception = exception;

        public TokenRequestContext? RequestContext { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            RequestContext = requestContext;
            CancellationToken = cancellationToken;
            if (exception is not null) throw exception;
            return accessToken!.Value;
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new(GetToken(requestContext, cancellationToken));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> messages = new();

        public IReadOnlyCollection<string> Messages => messages.ToArray();
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(messages);
        public void Dispose() { }
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            messages.Enqueue(formatter(state, exception));
    }
}
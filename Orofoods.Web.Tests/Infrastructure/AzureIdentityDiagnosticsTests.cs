using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Orofoods.Web.Infrastructure.Diagnostics;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class AzureIdentityDiagnosticsTests
{
    [Fact]
    public void IsEnabled_is_false_by_default()
    {
        Assert.False(AzureIdentityDiagnostics.IsEnabled(Environment("Staging"), Configuration()));
    }

    [Fact]
    public void IsEnabled_is_false_outside_staging_even_when_flag_is_enabled()
    {
        var configuration = Configuration(("Diagnostics:AzureIdentity", "true"));

        Assert.False(AzureIdentityDiagnostics.IsEnabled(Environment("Development"), configuration));
        Assert.False(AzureIdentityDiagnostics.IsEnabled(Environment("Production"), configuration));
    }

    [Fact]
    public void IsEnabled_requires_staging_and_the_explicit_flag()
    {
        var configuration = Configuration(("Diagnostics:AzureIdentity", "true"));

        Assert.True(AzureIdentityDiagnostics.IsEnabled(Environment("Staging"), configuration));
    }

    [Fact]
    public void Redact_removes_tokens_and_sensitive_values()
    {
        const string token = "eyJhbGciOiJIUzI1NiJ9.eyJvaWQiOiIxMjMifQ.signature";
        var message = AzureIdentityDiagnostics.Redact(
            $"Authorization: Bearer {token}; client_secret=secret-value; sig=signature-value");

        Assert.DoesNotContain(token, message);
        Assert.DoesNotContain("secret-value", message);
        Assert.DoesNotContain("signature-value", message);
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
}
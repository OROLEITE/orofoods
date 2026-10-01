using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace Orofoods.Web.Tests.Infrastructure;

/// <summary>
/// Adds the web project's User Secrets only to local integration configuration explicitly
/// running as Test. This helper is test-project scoped and is not used by the portal startup.
/// </summary>
internal static class LocalPointTestConfiguration
{
    public static IConfigurationBuilder AddUserSecretsOnlyForTest(
        this IConfigurationBuilder configuration,
        string environmentName,
        Assembly webProjectAssembly,
        Action<IConfigurationBuilder>? addUserSecrets = null)
    {
        if (string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase))
        {
            if (addUserSecrets is not null)
            {
                addUserSecrets(configuration);
            }
            else
            {
                configuration.AddUserSecrets(webProjectAssembly, optional: true);
            }
        }

        return configuration;
    }
}

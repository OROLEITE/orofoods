using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Orofoods.Web.Integrations.Erp.Wmc;

/// <summary>Cheap connectivity probe for the WMC Firebird mirror (SELECT 1 FROM RDB$DATABASE, nothing heavier).</summary>
public sealed class WmcFirebirdHealthCheck(IWmcFirebirdReader reader, IOptions<WmcFirebirdOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return HealthCheckResult.Healthy("Integracao WMC desativada.");
        }

        return await reader.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Conexao com o espelho Firebird da WMC OK.")
            : HealthCheckResult.Unhealthy("Nao foi possivel conectar ao espelho Firebird da WMC.");
    }
}

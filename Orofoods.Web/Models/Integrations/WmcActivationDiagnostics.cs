namespace Orofoods.Web.Models.Integrations;

public sealed record WmcActivationDiagnostics(
    int MaxOrderId,
    long TotalOrderCount,
    int? AutomaticWorkerStartAfterOrderId,
    bool WmcJsonOrderEnabled,
    bool AutomaticWorkerEnabled,
    bool WmcFileDropEnabled)
{
    public string AutomaticWorkerState =>
        !WmcJsonOrderEnabled || !AutomaticWorkerEnabled
            ? "Desativado pela configuração"
            : AutomaticWorkerStartAfterOrderId is not int cutoff || cutoff < 0
                ? "Bloqueado: marco ausente ou inválido"
                : "Habilitado pela configuração";
}

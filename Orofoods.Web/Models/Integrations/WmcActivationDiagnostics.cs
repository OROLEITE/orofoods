namespace Orofoods.Web.Models.Integrations;

public sealed record WmcActivationDiagnostics(
    int MaxOrderId,
    long TotalOrderCount,
    int? AutomaticWorkerStartAfterOrderId,
    bool WmcJsonOrderEnabled,
    bool AutomaticWorkerEnabled,
    bool WmcFileDropEnabled,
    WmcJsonOrderWorkerRuntimeSnapshot WorkerRuntime,
    WmcOrderEligibilityDiagnostic? OrderEligibility,
    string? DiagnosticInputError)
{
    public string AutomaticWorkerState =>
        !WmcJsonOrderEnabled || !AutomaticWorkerEnabled
            ? "Desativado pela configuração"
            : AutomaticWorkerStartAfterOrderId is not int cutoff || cutoff < 0
                ? "Bloqueado: marco ausente ou inválido"
                : "Habilitado pela configuração";

    public string AutomaticWorkerRuntimeState => !WorkerRuntime.IsServiceRunning
        ? WorkerRuntime.ServiceStartedAtUtc is null
            ? "Não observado nesta instância"
            : "Parado"
        : WorkerRuntime.CurrentCycleStartedAtUtc is not null
            ? "Executando ciclo"
            : "Serviço ativo; aguardando próximo ciclo";
}

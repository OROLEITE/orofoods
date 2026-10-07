using System.Text.Json.Serialization;

namespace Orofoods.Web.Services.Payments;

public interface IMercadoPagoPointTerminalDiscovery
{
    Task<IReadOnlyList<MercadoPagoPointTerminal>> ListTerminalsAsync(CancellationToken cancellationToken = default);

    Task<MercadoPagoPointTerminalModeChangeResult> SetTerminalOperatingModeAsync(
        string terminalId,
        string operatingMode,
        CancellationToken cancellationToken = default);
}

public sealed record MercadoPagoPointTerminal(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("store_id")]
    string? StoreId,
    [property: JsonPropertyName("pos_id")]
    string? PosId,
    [property: JsonPropertyName("external_pos_id")]
    string? ExternalPosId,
    [property: JsonPropertyName("operating_mode")]
    string? OperatingMode);

public sealed record MercadoPagoPointTerminalModeChangeResult(
    [property: JsonPropertyName("terminal_id")]
    string TerminalId,
    [property: JsonPropertyName("previous_operating_mode")]
    string PreviousOperatingMode,
    [property: JsonPropertyName("operating_mode")]
    string OperatingMode);

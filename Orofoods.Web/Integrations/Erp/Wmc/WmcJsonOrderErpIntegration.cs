using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Orofoods.Web.Integrations.Erp;
using Orofoods.Web.Models.Integrations;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Services.Integrations;

namespace Orofoods.Web.Integrations.Erp.Wmc;

public sealed class WmcJsonOrderErpIntegration(
    HttpClient httpClient,
    IOptions<WmcJsonOrderOptions> options,
    WmcMobileOrderPayloadBuilder payloadBuilder,
    WmcExportAuditService auditService,
    IWmcTransmissionIdAllocator transmissionIdAllocator,
    IHostEnvironment? environment = null) : IErpOrderIntegration
{
    public Task<ErpOrderResult> SendOrderAsync(Order order, CancellationToken cancellationToken = default, Guid? attemptId = null) =>
        SendOrderCoreAsync(order, WmcExportSource.Automatic, WmcExportAudit.AutomatedAttemptMarker, null, false, cancellationToken, attemptId);

    public Task<ErpOrderResult> SendAutomaticWorkerOrderAsync(
        Order order,
        CancellationToken cancellationToken = default,
        Guid? attemptId = null) =>
        SendOrderCoreAsync(order, WmcExportSource.Automatic, WmcExportAudit.AutomatedAttemptMarker, null, true, cancellationToken, attemptId);

    public Task<ErpOrderResult> SendManualOrderAsync(
        Order order,
        string? initiatedByUserId,
        string? initiatedByEmail,
        CancellationToken cancellationToken = default,
        Guid? attemptId = null) =>
        SendOrderCoreAsync(order, WmcExportSource.Manual, initiatedByEmail, initiatedByUserId, false, cancellationToken, attemptId);

    private async Task<ErpOrderResult> SendOrderCoreAsync(
        Order order,
        WmcExportSource source,
        string? initiatedByEmail,
        string? initiatedByUserId,
        bool automaticWorker,
        CancellationToken cancellationToken,
        Guid? attemptId)
    {
        var resolvedAttemptId = attemptId ?? Guid.NewGuid();
        var settings = options.Value;
        var audit = new WmcExportAudit
        {
            OrderId = order.Id,
            AttemptId = resolvedAttemptId,
            Source = source,
            Outcome = WmcExportOutcome.Started,
            Succeeded = false,
            ExportedByUserId = initiatedByUserId,
            ExportedByEmail = initiatedByEmail,
            ExportedAt = DateTime.UtcNow
        };
        await auditService.BeginAttemptAsync(audit, cancellationToken);

        if (!settings.Enabled)
        {
            const string error = "WMC JSON integration is disabled.";
            await auditService.CompleteAttemptAsync(order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, error, cancellationToken);
            return new ErpOrderResult(false, Error: error);
        }

        if (automaticWorker && (!settings.AutomaticWorkerEnabled || order.Status != OrderStatus.Approved))
        {
            const string error = "WMC automatic JSON processing requires the enabled worker and an approved order.";
            await auditService.CompleteAttemptAsync(order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, error, cancellationToken);
            return new ErpOrderResult(false, Error: error);
        }

        if (source == WmcExportSource.Manual && !settings.ManualImportEnabled)
        {
            const string error = "WMC manual JSON import is disabled.";
            await auditService.CompleteAttemptAsync(order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, error, cancellationToken);
            return new ErpOrderResult(false, Error: error);
        }

        if (source == WmcExportSource.Automatic && !automaticWorker &&
            (settings.HomologationOrderId is not int selectedOrderId ||
             selectedOrderId <= 0 ||
             order.Id != selectedOrderId ||
             !settings.HomologationOrderIdConfirmedUnique))
        {
            const string error = "Transmission requires the explicitly selected Order.Id and confirmed WMC uniqueness.";
            await auditService.CompleteAttemptAsync(order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, error, cancellationToken);
            return new ErpOrderResult(false, Error: error);
        }

        if (!TryGetAllowedBaseUri(settings.BaseUrl, out var baseUri))
        {
            const string error = "WMC JSON base URL must use HTTPS, except for the documented WMC HTTP endpoint, and must not contain credentials, a query, or a fragment.";
            await auditService.CompleteAttemptAsync(order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, error, cancellationToken);
            return new ErpOrderResult(false, Error: error);
        }

        int transmissionId;
        string payload;
        int sellerCode;
        string sellerSerial;
        try
        {
            transmissionId = await transmissionIdAllocator.GetOrAllocateAsync(order, cancellationToken);
            await auditService.RecordTransmissionIdAsync(order.Id, resolvedAttemptId, transmissionId, cancellationToken);
            payload = payloadBuilder.Build(order, transmissionId, settings.SalesRepresentativeCodes);
            if (string.IsNullOrWhiteSpace(settings.DeviceId))
            {
                throw new WmcOrderMappingException("O IMEI/dispositivo emitido pelo WMC não está configurado.");
            }

            var sellerId = order.Customer!.SalesRepresentativeId!.Value;
            sellerCode = settings.SalesRepresentativeCodes[sellerId];
            if (!settings.SalesRepresentativeSerials.TryGetValue(sellerId, out var configuredSellerSerial) ||
                string.IsNullOrWhiteSpace(configuredSellerSerial))
            {
                throw new WmcOrderMappingException("O serial WMC do vendedor do pedido não está configurado.");
            }
            sellerSerial = configuredSellerSerial;

            await auditService.RecordTransmissionContextAsync(
                order.Id,
                resolvedAttemptId,
                transmissionId,
                sellerCode,
                baseUri.ToString().TrimEnd('/'),
                cancellationToken);
        }
        catch (WmcOrderMappingException exception)
        {
            await auditService.CompleteAttemptAsync(order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, exception.Message, CancellationToken.None);
            return new ErpOrderResult(false, Error: exception.Message);
        }
        catch (Exception exception)
        {
            const string error = "WMC transmission preparation failed before any HTTP request.";
            await auditService.CompleteAttemptAsync(
                order.Id, resolvedAttemptId, WmcExportOutcome.Failed, null, null, $"{error} {exception.Message}", CancellationToken.None);
            return new ErpOrderResult(false, Error: error);
        }

        var exportUri = new Uri(
            baseUri.ToString().TrimEnd('/') + "/exportar.php?imei=" + Uri.EscapeDataString(settings.DeviceId.Trim()) +
            "&serial=" + Uri.EscapeDataString(sellerSerial.Trim()),
            UriKind.Absolute);

        var requestStarted = false;
        try
        {
            using var content = WmcMobileFormUrlEncoder.Create("HTTP_JSON", payload);
            requestStarted = true;
            using var response = await httpClient.PostAsync(exportUri, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var succeeded = response.StatusCode == HttpStatusCode.OK &&
                string.Equals(responseBody.Trim(), "importado com sucesso", StringComparison.Ordinal);
            var outcome = succeeded ? WmcExportOutcome.Succeeded : WmcExportOutcome.Unknown;
            var error = succeeded ? null : "WMC response did not confirm the import unambiguously; automatic resend is blocked.";
            try
            {
                await auditService.CompleteAttemptAsync(
                    order.Id, resolvedAttemptId, outcome, (int)response.StatusCode, responseBody, error, CancellationToken.None);
            }
            catch
            {
                return new ErpOrderResult(false, Error: "WMC response could not be recorded; automatic resend is blocked.", OutcomeUnknown: true);
            }

            if (!succeeded)
            {
                return await ReconcileAmbiguousTransmissionAsync(
                    order,
                    resolvedAttemptId,
                    baseUri,
                    transmissionId,
                    sellerCode,
                    error!,
                    cancellationToken);
            }

            var lookup = await LookupNroPedidoAsync(baseUri, transmissionId, sellerCode, cancellationToken);
            try
            {
                await auditService.RecordNroPedidoLookupAsync(
                    order.Id,
                    resolvedAttemptId,
                    lookup.HttpStatusCode,
                    lookup.ResponseBody,
                    lookup.Error,
                    lookup.OrderNumber?.ToString(CultureInfo.InvariantCulture),
                    CancellationToken.None);
            }
            catch
            {
                // The exact export success response is already audited; a lookup audit failure cannot make the POST retryable.
            }

            return new ErpOrderResult(
                true,
                ExternalOrderId: transmissionId.ToString(CultureInfo.InvariantCulture),
                ErpOrderNumber: lookup.OrderNumber?.ToString(CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (requestStarted)
        {
            var error = "WMC transmission outcome is uncertain; automatic resend is blocked.";
            try
            {
                await auditService.CompleteAttemptAsync(
                    order.Id, resolvedAttemptId, WmcExportOutcome.Unknown, null, null, $"{error} {exception.Message}", CancellationToken.None);
            }
            catch
            {
                // The persisted Started attempt and Processing order continue to block automatic retransmission.
            }

            return await ReconcileAmbiguousTransmissionAsync(
                order,
                resolvedAttemptId,
                baseUri,
                transmissionId,
                sellerCode,
                error,
                cancellationToken);
        }
    }

    private async Task<ErpOrderResult> ReconcileAmbiguousTransmissionAsync(
        Order order,
        Guid attemptId,
        Uri baseUri,
        int transmissionId,
        int sellerCode,
        string transmissionError,
        CancellationToken cancellationToken)
    {
        var lookup = await LookupNroPedidoAsync(baseUri, transmissionId, sellerCode, cancellationToken);
        try
        {
            await auditService.RecordNroPedidoLookupAsync(
                order.Id,
                attemptId,
                lookup.HttpStatusCode,
                lookup.ResponseBody,
                lookup.Error,
                lookup.OrderNumber?.ToString(CultureInfo.InvariantCulture),
                CancellationToken.None);
        }
        catch
        {
            // Preserve the Unknown export attempt; it continues to block automatic retransmission.
        }

        if (lookup.OrderNumber is > 0)
        {
            return new ErpOrderResult(
                true,
                ExternalOrderId: transmissionId.ToString(CultureInfo.InvariantCulture),
                ErpOrderNumber: lookup.OrderNumber.Value.ToString(CultureInfo.InvariantCulture));
        }

        return new ErpOrderResult(
            false,
            Error: $"{transmissionError} nropedido.php nÃ£o confirmou um nÃºmero; reenvio bloqueado.",
            OutcomeUnknown: true);
    }

    public async Task<WmcOrderNumberReconciliationResult> ReconcileNroPedidoAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var environmentBlocksReconciliation = environment is not null
            && !environment.IsDevelopment()
            && !environment.IsStaging()
            && !environment.IsEnvironment("Test");
        if (!settings.Enabled || environmentBlocksReconciliation && !settings.ManualImportEnabled)
        {
            return new WmcOrderNumberReconciliationResult(false, null, "WMC JSON integration is disabled.");
        }

        var target = await auditService.GetOrderNumberReconciliationTargetAsync(orderId, cancellationToken);
        if (target is null || target.Outcome == WmcExportOutcome.Succeeded &&
            !string.Equals(target.ExportResponseBody?.Trim(), "importado com sucesso", StringComparison.Ordinal))
        {
            return new WmcOrderNumberReconciliationResult(false, null, "Pedido sem exportação WMC confirmada e pendente de número.");
        }

        if (target.WmcTransmissionId <= 0 ||
            target.SalesRepresentativeCode <= 0 ||
            target.SalesRepresentativeCode > short.MaxValue)
        {
            const string error = "Identificadores persistidos na auditoria são incompatíveis com o contrato do WMC Mobile.";
            await auditService.RecordNroPedidoLookupAsync(
                orderId,
                target.AttemptId,
                null,
                null,
                error,
                null,
                cancellationToken);
            return new WmcOrderNumberReconciliationResult(false, null, error);
        }

        NroPedidoLookupResult lookup;
        if (!TryGetAllowedBaseUri(target.Destination, out var baseUri))
        {
            lookup = new NroPedidoLookupResult(null, null, null, "Destino WMC salvo na auditoria inválido para reconciliação.");
        }
        else
        {
            lookup = await LookupNroPedidoAsync(
                baseUri,
                target.WmcTransmissionId,
                target.SalesRepresentativeCode,
                cancellationToken);
        }

        try
        {
            await auditService.RecordNroPedidoLookupAsync(
                orderId,
                target.AttemptId,
                lookup.HttpStatusCode,
                lookup.ResponseBody,
                lookup.Error,
                lookup.OrderNumber?.ToString(CultureInfo.InvariantCulture),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new WmcOrderNumberReconciliationResult(
                false,
                null,
                $"A resposta de nropedido.php não foi persistida: {exception.Message}");
        }

        return new WmcOrderNumberReconciliationResult(
            lookup.OrderNumber is > 0,
            lookup.OrderNumber?.ToString(CultureInfo.InvariantCulture),
            lookup.Error);
    }

    private async Task<NroPedidoLookupResult> LookupNroPedidoAsync(
        Uri baseUri,
        int transmissionId,
        int sellerCode,
        CancellationToken cancellationToken)
    {
        var lookupUri = new Uri(
            baseUri.ToString().TrimEnd('/') + "/nropedido.php?idpedido=" +
            transmissionId.ToString(CultureInfo.InvariantCulture) + "&codvendedor=" +
            sellerCode.ToString(CultureInfo.InvariantCulture),
            UriKind.Absolute);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, lookupUri);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = Encoding.Latin1.GetString(await response.Content.ReadAsByteArrayAsync(cancellationToken));
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return new NroPedidoLookupResult(
                    null,
                    (int)response.StatusCode,
                    responseBody,
                    $"nropedido.php responded with HTTP {(int)response.StatusCode}.");
            }

            if (TryReadNroPedido(responseBody, out var orderNumber))
            {
                return new NroPedidoLookupResult(orderNumber, (int)response.StatusCode, responseBody, null);
            }

            return new NroPedidoLookupResult(
                null,
                (int)response.StatusCode,
                responseBody,
                "nropedido.php response did not contain a positive NROPEDIDO in the APK JSON array shape.");
        }
        catch (Exception exception)
        {
            return new NroPedidoLookupResult(null, null, null, $"nropedido.php lookup failed: {exception.Message}");
        }
    }

    private static bool TryReadNroPedido(string responseBody, out int orderNumber)
    {
        orderNumber = default;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (document.RootElement.ValueKind != JsonValueKind.Array ||
                document.RootElement.GetArrayLength() == 0)
            {
                return false;
            }

            var first = document.RootElement[0];
            if (first.ValueKind != JsonValueKind.Object ||
                !first.TryGetProperty("NROPEDIDO", out var value))
            {
                return false;
            }

            if (value.ValueKind == JsonValueKind.Number)
            {
                return value.TryGetInt32(out orderNumber) && orderNumber > 0;
            }

            return value.ValueKind == JsonValueKind.String &&
                int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out orderNumber) &&
                orderNumber > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetAllowedBaseUri(string? value, out Uri baseUri)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            baseUri = null!;
            return false;
        }

        if (parsed.Scheme == Uri.UriSchemeHttps ||
            parsed.Scheme == Uri.UriSchemeHttp &&
            string.Equals(parsed.Host, "mobileopt.wmcsistemas.com.br", StringComparison.OrdinalIgnoreCase) &&
            parsed.Port == 8084 &&
            parsed.AbsolutePath.TrimEnd('/') == "/oroleite")
        {
            baseUri = parsed;
            return true;
        }

        baseUri = null!;
        return false;
    }

    private sealed record NroPedidoLookupResult(int? OrderNumber, int? HttpStatusCode, string? ResponseBody, string? Error);
}

public sealed record WmcOrderNumberReconciliationResult(bool Recovered, string? ErpOrderNumber, string? Error);

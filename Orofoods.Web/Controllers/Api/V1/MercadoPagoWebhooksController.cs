using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Controllers.Api.V1;

/// <summary>
/// Public Mercado Pago order webhook. Every request is signature-validated before the authoritative
/// order state is re-queried from Mercado Pago; the request payload is never trusted for amount/status.
/// </summary>
[ApiController]
[Route("api/v1/webhooks/mercadopago")]
[AllowAnonymous]
[EnableRateLimiting("api")]
public class MercadoPagoWebhooksController(
    IMercadoPagoWebhookSignatureValidator signatureValidator,
    PaymentOrchestrationService orchestrationService,
    ILogger<MercadoPagoWebhooksController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var signature = Request.Headers["x-signature"].ToString();
        var requestId = Request.Headers["x-request-id"].ToString();
        var dataId = Request.Query["data.id"].ToString();
        var type = Request.Query["type"].ToString();

        if (string.IsNullOrWhiteSpace(dataId))
        {
            try
            {
                var payload = await JsonSerializer.DeserializeAsync<MercadoPagoWebhookPayload>(Request.Body, cancellationToken: cancellationToken);
                dataId = payload?.Data?.Id;
                if (string.IsNullOrWhiteSpace(type))
                {
                    type = payload?.Type;
                }
            }
            catch (JsonException)
            {
                return BadRequest();
            }
        }

        if (string.IsNullOrWhiteSpace(dataId))
        {
            logger.LogWarning("Rejected Mercado Pago webhook without data.id. RequestId={RequestId}", requestId);
            return BadRequest();
        }

        if (!signatureValidator.IsValid(signature, requestId, dataId))
        {
            logger.LogWarning("Rejected Mercado Pago webhook with invalid signature. RequestId={RequestId} GatewayOrderId={GatewayOrderId}", requestId, dataId);
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "order", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Ignored Mercado Pago webhook of unsupported type. Type={Type} RequestId={RequestId} GatewayOrderId={GatewayOrderId}", type, requestId, dataId);
            return Ok();
        }

        try
        {
            var applied = await orchestrationService.ReconcileMercadoPagoOrderAsync(dataId, cancellationToken);
            logger.LogInformation(
                "Processed Mercado Pago webhook. GatewayOrderId={GatewayOrderId} RequestId={RequestId} Applied={Applied}",
                dataId, requestId, applied);
        }
        catch (UnknownMercadoPagoOrderException)
        {
            logger.LogWarning("Mercado Pago webhook references an order not yet persisted locally. GatewayOrderId={GatewayOrderId} RequestId={RequestId}", dataId, requestId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning("Mercado Pago webhook reconciliation failed validation. GatewayOrderId={GatewayOrderId} RequestId={RequestId} Reason={Reason}", dataId, requestId, ex.Message);
            return UnprocessableEntity();
        }
        catch (Exception ex) when (ex is PaymentGatewayException or HttpRequestException or TimeoutException or OperationCanceledException)
        {
            logger.LogWarning("Mercado Pago webhook reconciliation could not reach the authoritative gateway state. GatewayOrderId={GatewayOrderId} RequestId={RequestId} ErrorType={ErrorType}", dataId, requestId, ex.GetType().Name);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return Ok();
    }

    private sealed class MercadoPagoWebhookPayload
    {
        [JsonPropertyName("type")] public string? Type { get; init; }
        [JsonPropertyName("data")] public MercadoPagoWebhookData? Data { get; init; }
    }

    private sealed class MercadoPagoWebhookData
    {
        [JsonPropertyName("id")] public string? Id { get; init; }
    }
}

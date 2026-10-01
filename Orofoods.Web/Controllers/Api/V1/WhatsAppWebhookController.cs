using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Controllers.Api.V1;

[ApiController]
[AllowAnonymous]
public class WhatsAppWebhookController(WhatsAppConversationService service, IOptions<WhatsAppBusinessOptions> options) : ControllerBase
{
    public const int MaxPayloadBytes = 1_048_576;

    [HttpGet("/api/webhooks/whatsapp")]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? verifyToken, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (!options.Value.Enabled) return NotFound();
        return mode == "subscribe" && !string.IsNullOrWhiteSpace(challenge) && TokensMatch(verifyToken, options.Value.VerifyToken)
            ? Content(challenge, "text/plain")
            : StatusCode(StatusCodes.Status403Forbidden);
    }

    [RequestSizeLimit(MaxPayloadBytes)]
    [HttpPost("/api/webhooks/whatsapp")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return NotFound();
        if (Request.ContentLength > MaxPayloadBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        var body = await ReadBodyWithinLimitAsync(Request.Body, cancellationToken);
        if (body is null) return StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (!service.IsValidSignature(body, Request.Headers["X-Hub-Signature-256"].FirstOrDefault())) return Unauthorized();
        try
        {
            using var document = JsonDocument.Parse(body);
            await service.ProcessAsync(document, cancellationToken);
            return Ok();
        }
        catch (JsonException) { return BadRequest(); }
        catch (WhatsAppWebhookPayloadException) { return BadRequest(); }
        catch (WhatsAppWebhookSourceMismatchException) { return StatusCode(StatusCodes.Status403Forbidden); }
    }

    private static bool TokensMatch(string? supplied, string? configured)
    {
        if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrWhiteSpace(configured)) return false;
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(supplied),
            System.Text.Encoding.UTF8.GetBytes(configured));
    }

    private static async Task<byte[]?> ReadBodyWithinLimitAsync(Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        while (true)
        {
            var remainingIncludingOverflowByte = MaxPayloadBytes + 1L - buffer.Length;
            if (remainingIncludingOverflowByte <= 0) return null;

            var bytesRead = await body.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, remainingIncludingOverflowByte)), cancellationToken);
            if (bytesRead == 0) return buffer.ToArray();

            buffer.Write(chunk, 0, bytesRead);
            if (buffer.Length > MaxPayloadBytes) return null;
        }
    }
}

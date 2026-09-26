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
    [HttpGet("/api/webhooks/whatsapp")]
    public IActionResult Verify([FromQuery(Name = "hub.mode")] string? mode, [FromQuery(Name = "hub.verify_token")] string? verifyToken, [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (!options.Value.Enabled) return NotFound();
        return mode == "subscribe" && !string.IsNullOrWhiteSpace(challenge) && verifyToken == options.Value.VerifyToken ? Content(challenge, "text/plain") : Forbid();
    }

    [HttpPost("/api/webhooks/whatsapp")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return NotFound();
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (!service.IsValidSignature(body, Request.Headers["X-Hub-Signature-256"].FirstOrDefault())) return Unauthorized();
        try
        {
            using var document = JsonDocument.Parse(body);
            await service.ProcessAsync(document, cancellationToken);
            return Ok();
        }
        catch (JsonException) { return BadRequest(); }
    }
}

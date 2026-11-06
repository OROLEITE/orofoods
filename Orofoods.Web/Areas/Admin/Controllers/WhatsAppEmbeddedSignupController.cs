using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Orofoods.Web.Areas.Admin.Models;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Commercial;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = ApplicationRoles.Administrator)]
public sealed class WhatsAppEmbeddedSignupController(
    WhatsAppBusinessConnectionService connectionService,
    IOptions<WhatsAppBusinessOptions> options) : Controller
{
    [HttpGet("/Admin/WhatsApp/Connect")]
    public IActionResult Connect()
    {
        var settings = options.Value;
        if (!settings.EmbeddedSignupEnabled)
            return NotFound();

        var message = TempData["WhatsAppEmbeddedSignupMessage"] as string;
        return View("~/Areas/Admin/Views/WhatsApp/EmbeddedSignup.cshtml", new WhatsAppEmbeddedSignupViewModel(
            settings.IsEmbeddedSignupReady,
            settings.EmbeddedSignupAppId,
            settings.EmbeddedSignupConfigId,
            settings.GraphApiVersion,
            settings.EmbeddedSignupDisplayPhoneNumber,
            message));
    }

    [HttpPost("/Admin/WhatsApp/CompleteEmbeddedSignup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(
        [FromForm] WhatsAppEmbeddedSignupCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!options.Value.EmbeddedSignupEnabled)
            return NotFound();

        if (request.EventType is null || request.EventType.Length > 100 ||
            request.AuthorizationCode is null || request.AuthorizationCode.Length > 4096 ||
            request.BusinessAccountId is null || request.BusinessAccountId.Length > 128)
            return BadRequest();

        try
        {
            var result = await connectionService.CompleteAsync(
                request.EventType,
                request.AuthorizationCode,
                request.BusinessAccountId,
                cancellationToken);
            TempData["WhatsAppEmbeddedSignupMessage"] = result.HistorySyncRequested
                ? "WhatsApp Business conectado. A sincronização do histórico foi solicitada; a sincronização de contatos permanece bloqueada até validação oficial da Meta."
                : "WhatsApp Business conectado. A sincronização do histórico precisa ser verificada; a sincronização de contatos permanece bloqueada até validação oficial da Meta.";
            return RedirectToAction(nameof(Connect));
        }
        catch (WhatsAppEmbeddedSignupValidationException)
        {
            return BadRequest();
        }
        catch (InvalidOperationException)
        {
            TempData["WhatsAppEmbeddedSignupMessage"] = "A Meta não concluiu a conexão. Nenhuma credencial foi exibida.";
            return RedirectToAction(nameof(Connect));
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public sealed class PaymentTerminalsController(
    ApplicationDbContext db,
    IDriverPaymentTerminalService assignmentService,
    IMercadoPagoPointTerminalDiscovery terminalDiscovery,
    IHostEnvironment hostEnvironment,
    ILogger<PaymentTerminalsController> logger) : Controller
{
    public async Task<IActionResult> Index() => View(await db.PaymentTerminals.AsNoTracking().OrderBy(x => x.Provider).ThenBy(x => x.DeviceId).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> DiscoverMercadoPagoTerminals(CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsStaging()) return NotFound();

        try
        {
            return Ok(await terminalDiscovery.ListTerminalsAsync(cancellationToken));
        }
        catch (PaymentGatewayException exception)
        {
            logger.LogWarning("Mercado Pago Point terminal discovery returned HTTP {StatusCode}.", (int)exception.StatusCode);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Não foi possível consultar os terminais Mercado Pago." });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Mercado Pago Point terminal discovery failed. ErrorType={ErrorType}", exception.GetType().Name);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Não foi possível consultar os terminais Mercado Pago." });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Mercado Pago Point terminal discovery timed out.");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "A consulta de terminais Mercado Pago excedeu o tempo limite." });
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Mercado Pago Point terminal discovery is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "A consulta de terminais Mercado Pago não está configurada." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetMercadoPagoTerminalOperatingModeToPdv(CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsStaging()) return NotFound();

        try
        {
            var result = await terminalDiscovery.SetTerminalOperatingModeAsync(
                MercadoPagoPointTerminalDiscovery.AuthorizedStagingTerminalId,
                "PDV",
                cancellationToken);
            return Ok(result);
        }
        catch (PaymentGatewayException exception)
        {
            logger.LogWarning("Mercado Pago Point terminal mode update returned HTTP {StatusCode}.", (int)exception.StatusCode);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Não foi possível alterar o modo do terminal Mercado Pago." });
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning("Mercado Pago Point terminal mode update failed. ErrorType={ErrorType}", exception.GetType().Name);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Não foi possível alterar o modo do terminal Mercado Pago." });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Mercado Pago Point terminal mode update timed out; no automatic retry was attempted.");
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "A alteração do modo do terminal excedeu o tempo limite. Verifique o estado antes de tentar novamente." });
        }
        catch (InvalidOperationException)
        {
            logger.LogWarning("Mercado Pago Point terminal mode update precondition or configuration failed.");
            return Conflict(new { message = "A alteração do modo do terminal não foi confirmada. Consulte o estado antes de qualquer nova tentativa." });
        }
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return View(new PaymentTerminalEditViewModel());
        var terminal = await db.PaymentTerminals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value);
        return terminal is null ? NotFound() : View(ToViewModel(terminal));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PaymentTerminalEditViewModel input, CancellationToken cancellationToken = default)
    {
        input.DeviceId = Normalize(input.DeviceId);
        input.StoreId = Normalize(input.StoreId);
        input.PosId = Normalize(input.PosId);
        if (!Enum.IsDefined(input.Provider)) ModelState.AddModelError(nameof(input.Provider), "Provedor inválido.");
        if (!ModelState.IsValid) return View(input);

        var result = input.Id == 0
            ? await assignmentService.CreateTerminalAsync(input.Provider, input.DeviceId, input.StoreId, input.PosId, input.IsActive, cancellationToken)
            : await assignmentService.UpdateTerminalAsync(input.Id, input.Provider, input.DeviceId, input.StoreId, input.PosId, input.IsActive, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.ErrorMessage == "Terminal não encontrado.") return NotFound();
            ModelState.AddModelError(nameof(input.DeviceId), result.ErrorMessage ?? "Não foi possível salvar o terminal.");
            return View(input);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken = default)
    {
        var result = await assignmentService.DeactivateTerminalAsync(id, cancellationToken);
        if (!result.Succeeded) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static PaymentTerminalEditViewModel ToViewModel(PaymentTerminal terminal) => new()
    {
        Id = terminal.Id,
        Provider = terminal.Provider,
        DeviceId = terminal.DeviceId,
        StoreId = terminal.StoreId,
        PosId = terminal.PosId,
        IsActive = terminal.IsActive
    };
}

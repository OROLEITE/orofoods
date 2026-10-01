using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public sealed class DriverPaymentTerminalAssignmentsController(
    ApplicationDbContext db,
    IDriverPaymentTerminalService assignmentService) : Controller
{
    public async Task<IActionResult> Index(int? driverId, int? terminalId, CancellationToken cancellationToken = default)
    {
        ViewBag.DriverId = driverId;
        ViewBag.PaymentTerminalId = terminalId;
        return View(await assignmentService.GetHistoryAsync(driverId, terminalId, cancellationToken));
    }

    public async Task<IActionResult> Create(CancellationToken cancellationToken = default)
    {
        await PopulateChoicesAsync(cancellationToken);
        return View(new DriverPaymentTerminalAssignmentCreateViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DriverPaymentTerminalAssignmentCreateViewModel input, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            await PopulateChoicesAsync(cancellationToken);
            return View(input);
        }

        var result = await assignmentService.AssignAsync(input.DriverId, input.PaymentTerminalId, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Não foi possível associar o terminal.");
            await PopulateChoicesAsync(cancellationToken);
            return View(input);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id, CancellationToken cancellationToken = default)
    {
        var result = await assignmentService.EndAsync(id, cancellationToken);
        if (!result.Succeeded) return BadRequest(result.ErrorMessage);
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateChoicesAsync(CancellationToken cancellationToken)
    {
        ViewBag.Drivers = await db.Drivers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(cancellationToken);
        ViewBag.PaymentTerminals = await db.PaymentTerminals.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DeviceId).ToListAsync(cancellationToken);
    }
}

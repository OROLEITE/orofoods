using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Delivery;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = "Administrador")]
public sealed class DriversController(
    ApplicationDbContext db,
    IDriverPaymentTerminalService assignmentService,
    TimeProvider timeProvider) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Drivers.AsNoTracking().OrderBy(x => x.Name).ToListAsync());

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null) return View(new DriverEditViewModel());
        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id.Value);
        return driver is null ? NotFound() : View(ToViewModel(driver));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DriverEditViewModel input, CancellationToken cancellationToken = default)
    {
        input.Name = input.Name?.Trim() ?? "";
        if (!ModelState.IsValid || input.Name.Length == 0) return View(input);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (input.Id == 0)
        {
            db.Drivers.Add(new Driver { Name = input.Name, IsActive = input.IsActive, CreatedAt = now, UpdatedAt = now });
        }
        else
        {
            var driver = await db.Drivers.SingleOrDefaultAsync(x => x.Id == input.Id, cancellationToken);
            if (driver is null) return NotFound();
            driver.Name = input.Name;
            if (driver.IsActive && !input.IsActive)
            {
                var result = await assignmentService.DeactivateDriverAsync(driver.Id, cancellationToken);
                if (!result.Succeeded) return BadRequest(result.ErrorMessage);
            }
            else
            {
                driver.IsActive = input.IsActive;
                driver.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        if (input.Id == 0) await db.SaveChangesAsync(cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken = default)
    {
        var result = await assignmentService.DeactivateDriverAsync(id, cancellationToken);
        if (!result.Succeeded) return NotFound();
        return RedirectToAction(nameof(Index));
    }

    private static DriverEditViewModel ToViewModel(Driver driver) => new() { Id = driver.Id, Name = driver.Name, IsActive = driver.IsActive };
}

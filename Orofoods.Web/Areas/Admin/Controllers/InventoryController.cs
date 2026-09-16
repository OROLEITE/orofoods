using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class InventoryController(ApplicationDbContext db, UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Products
        .AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .GroupJoin(db.ProductInventories, product => product.Id, inventory => inventory.ProductId, (product, inventories) => new InventoryRow(
            product.Id, product.Sku, product.Name, inventories.Select(x => (int?)x.QuantityOnHand).FirstOrDefault() ?? 0,
            inventories.Select(x => (int?)x.QuantityReserved).FirstOrDefault() ?? 0))
        .ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int productId, int quantityOnHand, string reason)
    {
        if (quantityOnHand < 0 || string.IsNullOrWhiteSpace(reason))
        {
            TempData["InventoryError"] = "Informe um saldo valido e o motivo do ajuste.";
            return RedirectToAction(nameof(Index));
        }

        var inventory = await db.ProductInventories.SingleOrDefaultAsync(x => x.ProductId == productId);
        if (inventory is null)
        {
            inventory = new ProductInventory { ProductId = productId };
            db.ProductInventories.Add(inventory);
        }

        if (quantityOnHand < inventory.QuantityReserved)
        {
            TempData["InventoryError"] = "O saldo fisico nao pode ser menor que o saldo reservado.";
            return RedirectToAction(nameof(Index));
        }

        var previousQuantity = inventory.QuantityOnHand;
        inventory.QuantityOnHand = quantityOnHand;
        db.InventoryAdjustments.Add(new InventoryAdjustment
        {
            ProductInventory = inventory,
            PreviousQuantityOnHand = previousQuantity,
            NewQuantityOnHand = quantityOnHand,
            Reason = reason.Trim(),
            AdjustedByUserId = userManager.GetUserId(User)
        });
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}

public sealed record InventoryRow(int ProductId, string Sku, string ProductName, int QuantityOnHand, int QuantityReserved)
{
    public int AvailableQuantity => QuantityOnHand - QuantityReserved;
}

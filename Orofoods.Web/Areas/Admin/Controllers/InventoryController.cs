using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Models.Inventory;

namespace Orofoods.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrador")]
public class InventoryController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ILogger<InventoryController> logger) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Products
        .AsNoTracking()
        .Where(x => x.IsActive)
        .OrderBy(x => x.Name)
        .GroupJoin(db.ProductInventories, product => product.Id, inventory => inventory.ProductId, (product, inventories) => new InventoryRow(
            product.Id, product.Sku, product.Name, product.Unit, product.UnitsPerCase,
            inventories.Select(x => (int?)x.QuantityOnHand).FirstOrDefault() ?? 0,
            inventories.Select(x => (int?)x.QuantityReserved).FirstOrDefault() ?? 0,
            inventories.Select(x => (bool?)x.IsManuallyAdjusted).FirstOrDefault() ?? false))
        .ToListAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Adjust(int productId, int quantityOnHand, string reason)
    {
        if (quantityOnHand < 0 || string.IsNullOrWhiteSpace(reason) || reason.Length > 300)
        {
            TempData["InventoryError"] = "Informe um saldo válido e um motivo de até 300 caracteres.";
            return RedirectToAction(nameof(Index));
        }

        if (!await db.Products.AnyAsync(product => product.Id == productId && product.IsActive))
        {
            TempData["InventoryError"] = "Produto ativo não encontrado para ajuste de estoque.";
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
            TempData["InventoryError"] = "O saldo físico não pode ser menor que o saldo reservado.";
            return RedirectToAction(nameof(Index));
        }

        var previousQuantity = inventory.QuantityOnHand;
        inventory.QuantityOnHand = quantityOnHand;
        inventory.IsManuallyAdjusted = true;
        db.InventoryAdjustments.Add(new InventoryAdjustment
        {
            ProductInventory = inventory,
            PreviousQuantityOnHand = previousQuantity,
            NewQuantityOnHand = quantityOnHand,
            Reason = reason.Trim(),
            AdjustedByUserId = userManager.GetUserId(User)
        });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Falha ao salvar ajuste de estoque para o produto {ProductId}.", productId);
            TempData["InventoryError"] = "Não foi possível salvar o ajuste. Confira o saldo e tente novamente.";
            return RedirectToAction(nameof(Index));
        }

        TempData["InventorySuccess"] = "Ajuste salvo. O saldo manual permanecerá protegido da sincronização WMC até a liberação explícita.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeWmcStockSync(int productId)
    {
        var inventory = await db.ProductInventories.SingleOrDefaultAsync(x => x.ProductId == productId);
        if (inventory is null)
        {
            TempData["InventoryError"] = "Saldo do produto não encontrado.";
            return RedirectToAction(nameof(Index));
        }

        inventory.IsManuallyAdjusted = false;
        await db.SaveChangesAsync();
        TempData["InventorySuccess"] = "A sincronização WMC poderá atualizar o saldo na próxima execução.";
        return RedirectToAction(nameof(Index));
    }
}

public sealed record InventoryRow(
    int ProductId,
    string Sku,
    string ProductName,
    string Unit,
    int UnitsPerCase,
    int QuantityOnHand,
    int QuantityReserved,
    bool IsManuallyAdjusted)
{
    public int AvailableQuantity => Math.Max(0, QuantityOnHand - QuantityReserved);
}

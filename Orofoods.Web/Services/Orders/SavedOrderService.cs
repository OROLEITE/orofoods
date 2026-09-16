using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Orders;

public sealed record SavedOrderLine(int ProductId, int Quantity);

public class SavedOrderService(ApplicationDbContext db)
{
    public async Task<SavedOrder> SaveAsync(int customerId, string name, IEnumerable<SavedOrderLine> lines, int? savedOrderId = null, CancellationToken cancellationToken = default)
    {
        var validLines = await GetLoadableLinesAsync(lines, cancellationToken);
        var savedOrder = savedOrderId is null
            ? new SavedOrder { CustomerId = customerId }
            : await db.SavedOrders.Include(x => x.Items).SingleAsync(x => x.Id == savedOrderId && x.CustomerId == customerId, cancellationToken);

        savedOrder.Name = name.Trim();
        savedOrder.IsActive = true;
        savedOrder.UpdatedAt = DateTime.UtcNow;
        savedOrder.Items.Clear();
        savedOrder.Items.AddRange(validLines.Select(x => new SavedOrderItem { ProductId = x.ProductId, Quantity = x.Quantity }));
        if (savedOrderId is null) db.SavedOrders.Add(savedOrder);
        await db.SaveChangesAsync(cancellationToken);
        return savedOrder;
    }

    public async Task<List<SavedOrderLine>> GetLoadableAsync(int customerId, int savedOrderId, CancellationToken cancellationToken = default)
    {
        var model = await db.SavedOrders.AsNoTracking().Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == savedOrderId && x.CustomerId == customerId && x.IsActive, cancellationToken);
        return model is null ? [] : await GetLoadableLinesAsync(model.Items.Select(x => new SavedOrderLine(x.ProductId, x.Quantity)), cancellationToken);
    }

    public Task<List<SavedOrder>> GetActiveAsync(int customerId, CancellationToken cancellationToken = default) =>
        db.SavedOrders.AsNoTracking().Include(x => x.Items).Where(x => x.CustomerId == customerId && x.IsActive)
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task<SavedOrder?> GetAsync(int customerId, int savedOrderId, CancellationToken cancellationToken = default) =>
        await db.SavedOrders.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Product)
            .SingleOrDefaultAsync(x => x.Id == savedOrderId && x.CustomerId == customerId && x.IsActive, cancellationToken);

    public async Task DeactivateAsync(int customerId, int savedOrderId, CancellationToken cancellationToken = default)
    {
        var model = await db.SavedOrders.SingleOrDefaultAsync(x => x.Id == savedOrderId && x.CustomerId == customerId, cancellationToken);
        if (model is null) return;
        model.IsActive = false;
        model.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<SavedOrderLine>> GetLoadableLinesAsync(IEnumerable<SavedOrderLine> lines, CancellationToken cancellationToken)
    {
        var requested = lines.Where(x => x.Quantity > 0).GroupBy(x => x.ProductId).Select(x => new SavedOrderLine(x.Key, x.Sum(item => item.Quantity))).ToList();
        var products = await db.Products.Where(x => requested.Select(item => item.ProductId).Contains(x.Id) && x.IsActive && x.IsAvailable)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        return requested.Where(x => products.ContainsKey(x.ProductId))
            .Select(x => new SavedOrderLine(x.ProductId, Math.Max(x.Quantity, products[x.ProductId].MinimumCases))).ToList();
    }
}

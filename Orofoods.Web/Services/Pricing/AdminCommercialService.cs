using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Pricing;

public class AdminCommercialService(ApplicationDbContext db)
{
    public async Task SavePriceAsync(int priceTableId, int productId, decimal price, decimal? promotionalPrice, CancellationToken cancellationToken = default)
    {
        if (price < 0 || promotionalPrice < 0) throw new InvalidOperationException("O preco nao pode ser negativo.");
        var item = await db.PriceTableItems.SingleOrDefaultAsync(x => x.PriceTableId == priceTableId && x.ProductId == productId, cancellationToken);
        if (item is null)
        {
            item = new PriceTableItem { PriceTableId = priceTableId, ProductId = productId };
            db.PriceTableItems.Add(item);
        }
        item.Price = price;
        item.PromotionalPrice = promotionalPrice;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SalesRepresentative> SaveSalesRepresentativeAsync(SalesRepresentative input, CancellationToken cancellationToken = default)
    {
        var entity = input.Id == 0 ? new SalesRepresentative() : await db.SalesRepresentatives.SingleAsync(x => x.Id == input.Id, cancellationToken);
        entity.Name = input.Name.Trim(); entity.Email = input.Email.Trim(); entity.Phone = input.Phone.Trim(); entity.IsActive = input.IsActive;
        if (input.Id == 0) db.SalesRepresentatives.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<PaymentTerm> SavePaymentTermAsync(PaymentTerm input, CancellationToken cancellationToken = default)
    {
        var entity = input.Id == 0 ? new PaymentTerm() : await db.PaymentTerms.SingleAsync(x => x.Id == input.Id, cancellationToken);
        entity.Name = input.Name.Trim(); entity.SortOrder = input.SortOrder; entity.IsActive = input.IsActive;
        if (input.Id == 0) db.PaymentTerms.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<PriceTable> SavePriceTableAsync(PriceTable input, CancellationToken cancellationToken = default)
    {
        var entity = input.Id == 0 ? new PriceTable() : await db.PriceTables.SingleAsync(x => x.Id == input.Id, cancellationToken);
        entity.Name = input.Name.Trim(); entity.IsActive = input.IsActive;
        if (input.Id == 0) db.PriceTables.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task DeactivateSalesRepresentativeAsync(int id) { var entity = await db.SalesRepresentatives.FindAsync(id); if (entity is not null) { entity.IsActive = false; await db.SaveChangesAsync(); } }
    public async Task DeactivatePaymentTermAsync(int id) { var entity = await db.PaymentTerms.FindAsync(id); if (entity is not null) { entity.IsActive = false; await db.SaveChangesAsync(); } }
    public async Task DeactivatePriceTableAsync(int id) { var entity = await db.PriceTables.FindAsync(id); if (entity is not null) { entity.IsActive = false; await db.SaveChangesAsync(); } }
}

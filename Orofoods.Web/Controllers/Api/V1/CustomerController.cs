using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Api;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Pricing;

namespace Orofoods.Web.Controllers.Api.V1;

[ApiController]
[Route("api/v1")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EnableRateLimiting("api")]
public class CustomerController(ApplicationDbContext db, PriceService priceService) : ControllerBase
{
    [HttpGet("customer")]
    public async Task<ActionResult> Current(CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().Include(x => x.CustomerPaymentTerms).ThenInclude(x => x.PaymentTerm).SingleOrDefaultAsync(x => x.Id == CustomerId, cancellationToken);
        return customer is null ? Forbid() : Ok(new { customer.Id, customer.LegalName, customer.TradeName, customer.MinimumOrder, customer.CreditLimit, customer.CreditUsed, PaymentTerms = customer.PaymentTerms });
    }

    [HttpGet("orders")]
    public async Task<ActionResult<PagedResult<object>>> Orders(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.Orders.AsNoTracking().Where(x => x.CustomerId == CustomerId);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAt).Skip(request.Skip).Take(request.PageSize)
            .Select(x => new { x.Id, x.Number, x.CreatedAt, x.Status, x.Total, x.IntegrationStatus, x.ErpOrderNumber, ItemCount = x.Items.Count }).ToListAsync(cancellationToken);
        return Ok(new PagedResult<object>(items.Cast<object>().ToList(), request.Page, request.PageSize, totalItems));
    }

    [HttpGet("prices")]
    public async Task<ActionResult<PagedResult<object>>> Prices(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name);
        var totalItems = await query.CountAsync(cancellationToken);
        var products = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(cancellationToken);
        var prices = await priceService.GetPricesAsync(CustomerId, products.Select(x => x.Id), cancellationToken);
        var items = products.Select(x => (object)new { x.Id, x.Sku, x.Name, Price = prices.GetValueOrDefault(x.Id, x.BasePrice) }).ToList();
        return Ok(new PagedResult<object>(items, request.Page, request.PageSize, totalItems));
    }

    [HttpGet("payment-terms")]
    public async Task<ActionResult<PagedResult<object>>> PaymentTerms(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.CustomerPaymentTerms.AsNoTracking().Where(x => x.CustomerId == CustomerId && x.IsActive && x.PaymentTerm!.IsActive);
        var totalItems = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.PaymentTerm!.SortOrder).Skip(request.Skip).Take(request.PageSize)
            .Select(x => (object)new { x.PaymentTerm!.Id, x.PaymentTerm.Name, x.PaymentTerm.SortOrder }).ToListAsync(cancellationToken);
        return Ok(new PagedResult<object>(items, request.Page, request.PageSize, totalItems));
    }

    private int CustomerId => int.TryParse(User.FindFirstValue("customer_id"), out var id) ? id : throw new UnauthorizedAccessException();
}

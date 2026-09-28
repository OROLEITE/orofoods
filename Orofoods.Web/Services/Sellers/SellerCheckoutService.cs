using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Services.Identity;
using Orofoods.Web.Services.Orders;
using Orofoods.Web.Services.Payments;
using Orofoods.Web.Services.Pricing;
using Orofoods.Web.ViewModels;

namespace Orofoods.Web.Services.Sellers;

public sealed class SellerCheckoutService(
    ApplicationDbContext db,
    SalesRepresentativeAccessService accessService,
    CartService cartService,
    IPaymentEligibilityService paymentEligibilityService,
    IOptions<MercadoPagoOptions>? mercadoPagoOptions = null)
{
    public async Task<SellerCheckoutViewModel?> GetAsync(
        ClaimsPrincipal user,
        int customerId,
        ISession session,
        CancellationToken cancellationToken = default)
    {
        var scope = await accessService.GetSellerCartScopeAsync(user, customerId, cancellationToken);
        if (scope is null) return null;

        var customer = await db.Customers.AsNoTracking()
            .Include(x => x.Addresses)
            .SingleOrDefaultAsync(x => x.Id == customerId && x.IsActive && x.Status == Models.Customers.CustomerStatus.Approved, cancellationToken);
        if (customer is null) return null;

        var eligibility = await paymentEligibilityService.GetAvailablePaymentOptionsAsync(customerId, cancellationToken);
        return new SellerCheckoutViewModel
        {
            CustomerId = customer.Id,
            CustomerCode = customer.WmcCode ?? customer.Cnpj,
            CustomerName = customer.TradeName,
            Cart = await cartService.GetAsync(customerId, session, scope),
            Addresses = customer.Addresses.Where(x => x.IsActive).ToList(),
            PaymentTerms = eligibility.PaymentMethods,
            PaymentEligibility = eligibility,
            MercadoPagoPublicKey = mercadoPagoOptions?.Value.PublicKey ?? ""
        };
    }
}
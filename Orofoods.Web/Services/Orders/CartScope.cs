namespace Orofoods.Web.Services.Orders;

public enum CartScopeKind
{
    CustomerSelfService,
    SellerAssisted
}

public sealed record CartScope
{
    private CartScope(CartScopeKind kind, int? salesRepresentativeId, int? customerId, string sessionSuffix)
    {
        Kind = kind;
        SalesRepresentativeId = salesRepresentativeId;
        CustomerId = customerId;
        SessionSuffix = sessionSuffix;
    }

    public CartScopeKind Kind { get; }
    public int? SalesRepresentativeId { get; }
    public int? CustomerId { get; }
    internal string SessionSuffix { get; }

    public static CartScope CustomerSelfService { get; } = new(CartScopeKind.CustomerSelfService, null, null, "");

    public static CartScope ForSeller(int salesRepresentativeId, int customerId)
    {
        if (salesRepresentativeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(salesRepresentativeId));
        }

        if (customerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(customerId));
        }

        return new(
            CartScopeKind.SellerAssisted,
            salesRepresentativeId,
            customerId,
            $"seller:{salesRepresentativeId}:customer:{customerId}");
    }
}
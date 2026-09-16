using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Data;

namespace Orofoods.Web.Services.Customers;

public class CustomerApprovalService(ApplicationDbContext db)
{
    public async Task ApplyAsync(CustomerApprovalRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers
            .Include(x => x.CustomerPaymentTerms)
            .SingleAsync(x => x.Id == request.CustomerId, cancellationToken);

        customer.Status = request.Status;
        customer.MinimumOrder = request.MinimumOrder;
        customer.CreditLimit = request.CreditLimit;
        customer.WmcCode = NormalizeWmcCode(request.WmcCode);
        customer.PriceTableId = request.PriceTableId;
        customer.SalesRepresentativeId = request.SalesRepresentativeId;
        customer.IsActive = request.Status != CustomerStatus.Inactive;
        customer.ApprovedAt = request.Status == CustomerStatus.Approved
            ? DateTime.UtcNow
            : null;

        db.CustomerPaymentTerms.RemoveRange(customer.CustomerPaymentTerms);
        customer.CustomerPaymentTerms.Clear();

        foreach (var paymentTermId in request.PaymentTermIds.Distinct())
        {
            customer.CustomerPaymentTerms.Add(new CustomerPaymentTerm
            {
                CustomerId = customer.Id,
                PaymentTermId = paymentTermId,
                IsActive = true
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? NormalizeWmcCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class CustomerApprovalRequest
{
    public int CustomerId { get; set; }
    public CustomerStatus Status { get; set; }
    public decimal MinimumOrder { get; set; }
    public decimal CreditLimit { get; set; }
    public string? WmcCode { get; set; }
    public int? PriceTableId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public IReadOnlyCollection<int> PaymentTermIds { get; set; } = [];
}

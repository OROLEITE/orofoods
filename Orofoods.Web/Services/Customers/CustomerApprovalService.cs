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
        customer.CreditOverrideEnabled = request.CreditOverrideEnabled;
        customer.MaximumPaymentTermDays = request.CreditOverrideEnabled ? Math.Clamp(request.MaximumPaymentTermDays ?? 0, 0, 14) : null;
        customer.CreditBlocked = request.CreditBlocked;
        customer.CreditNotes = string.IsNullOrWhiteSpace(request.CreditNotes) ? null : request.CreditNotes.Trim();
        if (request.CreditOverrideEnabled && customer.MaximumPaymentTermDays > 0 && customer.CreditReleaseDate is null)
        {
            customer.CreditReleaseDate = DateTime.UtcNow;
        }
        customer.WmcCode = NormalizeWmcCode(request.WmcCode);
        customer.PriceTableId = request.PriceTableId;
        customer.SalesRepresentativeId = request.SalesRepresentativeId;
        customer.InternalSalesUserId = request.InternalSalesUserId;
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
    public bool CreditOverrideEnabled { get; set; }
    public int? MaximumPaymentTermDays { get; set; }
    public bool CreditBlocked { get; set; }
    public string? CreditNotes { get; set; }
    public string? WmcCode { get; set; }
    public int? PriceTableId { get; set; }
    public int? SalesRepresentativeId { get; set; }
    public string? InternalSalesUserId { get; set; }
    public IReadOnlyCollection<int> PaymentTermIds { get; set; } = [];
}

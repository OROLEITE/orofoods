using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Services.Payments;

public sealed class PaymentService(
    ApplicationDbContext db,
    IOptions<PaymentEligibilityOptions> eligibilityOptions,
    IBoletoProvider boletoProvider)
{
    public async Task IssueForEligibleStatusAsync(int orderId, OrderStatus status, DateTime statusChangedAt, CancellationToken cancellationToken = default)
    {
        var baseDateStatus = eligibilityOptions.Value.PaymentTermBaseDate == PaymentTermBaseDate.InvoiceDate
            ? OrderStatus.Invoiced
            : OrderStatus.Delivered;
        if (status != baseDateStatus)
        {
            return;
        }

        var order = await db.Orders.Include(x => x.PaymentTerm).SingleAsync(x => x.Id == orderId, cancellationToken);
        if (order.PaymentTerm is null || order.PaymentTerm.DaysUntilDue <= 0)
        {
            return;
        }

        if (order.PaymentTerm.DaysUntilDue > eligibilityOptions.Value.AbsoluteMaximumTermDays)
        {
            throw new InvalidOperationException("O prazo do boleto ultrapassa o máximo permitido.");
        }

        var existing = await db.Payments.SingleOrDefaultAsync(x => x.OrderId == orderId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var dueDate = statusChangedAt.Date.AddDays(order.PaymentTerm.DaysUntilDue);
        var issued = await boletoProvider.CreateBankSlipAsync(
            new BoletoProviderRequest(order.Id, order.CustomerId, order.Total, dueDate),
            cancellationToken);
        db.Payments.Add(new Payment
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            PaymentMethod = order.PaymentTerm.Code,
            Method = PaymentMethodType.Boleto,
            Amount = order.Total,
            DueDate = issued.DueDate,
            Status = issued.Status,
            ExternalPaymentId = issued.ExternalPaymentId,
            DigitableLine = issued.DigitableLine,
            Barcode = issued.Barcode,
            BankSlipUrl = issued.BankSlipUrl,
            CreatedAt = statusChangedAt,
            UpdatedAt = statusChangedAt
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
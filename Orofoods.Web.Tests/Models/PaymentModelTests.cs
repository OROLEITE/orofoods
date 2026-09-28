using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Payments;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Models;

public class PaymentModelTests
{
    [Fact]
    public void Existing_payment_status_numeric_values_are_preserved()
    {
        Assert.Equal(0, (int)PaymentStatus.Pending);
        Assert.Equal(1, (int)PaymentStatus.Issued);
        Assert.Equal(2, (int)PaymentStatus.Paid);
        Assert.Equal(3, (int)PaymentStatus.Overdue);
        Assert.Equal(4, (int)PaymentStatus.Cancelled);
        Assert.Equal(5, (int)PaymentStatus.Failed);
    }

    [Fact]
    public void Gateway_payment_statuses_use_new_explicit_numeric_values()
    {
        Assert.Equal(6, (int)PaymentStatus.Processing);
        Assert.Equal(7, (int)PaymentStatus.Approved);
        Assert.Equal(8, (int)PaymentStatus.Rejected);
        Assert.Equal(9, (int)PaymentStatus.Refunded);
        Assert.Equal(10, (int)PaymentStatus.Expired);
    }

    [Fact]
    public async Task Payment_attempts_have_unique_idempotency_and_non_unique_internal_order_indexes()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var entity = db.Model.FindEntityType(typeof(Payment))!;

        var idempotencyIndex = entity.GetIndexes().Single(index => index.Properties.SingleOrDefault()?.Name == nameof(Payment.IdempotencyKey));
        var internalOrderIndex = entity.GetIndexes().Single(index => index.Properties.SingleOrDefault()?.Name == nameof(Payment.OrderId));

        Assert.True(idempotencyIndex.IsUnique);
        Assert.False(internalOrderIndex.IsUnique);
        Assert.Contains(entity.GetIndexes(), index => index.Properties.SingleOrDefault()?.Name == nameof(Payment.GatewayOrderId));
        Assert.Contains(entity.GetIndexes(), index => index.Properties.SingleOrDefault()?.Name == nameof(Payment.GatewayPaymentId));
    }
}

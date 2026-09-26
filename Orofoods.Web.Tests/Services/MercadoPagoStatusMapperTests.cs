using Orofoods.Web.Models.Payments;
using Orofoods.Web.Services.Payments;

namespace Orofoods.Web.Tests.Services;

public class MercadoPagoStatusMapperTests
{
    [Theory]
    [InlineData("created", "created", PaymentStatus.Pending)]
    [InlineData("processing", "in_process", PaymentStatus.Processing)]
    [InlineData("in_review", "in_review", PaymentStatus.Processing)]
    [InlineData("action_required", "waiting_payment", PaymentStatus.Pending)]
    [InlineData("processed", "accredited", PaymentStatus.Approved)]
    [InlineData("processed", "partially_refunded", PaymentStatus.Approved)]
    [InlineData("failed", "rejected_by_issuer", PaymentStatus.Rejected)]
    [InlineData("canceled", "canceled", PaymentStatus.Cancelled)]
    [InlineData("refunded", "refunded", PaymentStatus.Refunded)]
    [InlineData("expired", "expired", PaymentStatus.Expired)]
    public void Maps_official_order_statuses_to_internal_statuses(string status, string detail, PaymentStatus expected)
    {
        Assert.Equal(expected, MercadoPagoStatusMapper.Map(status, detail));
    }

    [Fact]
    public void Unknown_status_maps_to_failed_without_leaking_gateway_strings_into_callers()
    {
        Assert.Equal(PaymentStatus.Failed, MercadoPagoStatusMapper.Map("future_status", "future_detail"));
    }
}

namespace Orofoods.Web.Services.Payments;

public static class MercadoPagoStatusMapper
{
    public static PaymentStatus Map(string? status, string? statusDetail) =>
        (status?.Trim().ToLowerInvariant(), statusDetail?.Trim().ToLowerInvariant()) switch
        {
            ("created", _) => PaymentStatus.Pending,
            ("processing", _) => PaymentStatus.Processing,
            ("in_review", _) => PaymentStatus.Processing,
            ("action_required", _) => PaymentStatus.Pending,
            ("processed", "accredited") => PaymentStatus.Approved,
            ("processed", "partially_refunded") => PaymentStatus.Approved,
            ("processed", "refunded") => PaymentStatus.Refunded,
            ("canceled", _) => PaymentStatus.Cancelled,
            ("refunded", _) => PaymentStatus.Refunded,
            ("expired", _) => PaymentStatus.Expired,
            ("failed", _) => PaymentStatus.Rejected,
            _ => PaymentStatus.Failed
        };
}

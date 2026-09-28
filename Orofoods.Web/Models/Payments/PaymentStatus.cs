namespace Orofoods.Web.Models.Payments;

public enum PaymentStatus
{
    Pending = 0,
    Issued = 1,
    Paid = 2,
    Overdue = 3,
    Cancelled = 4,
    Failed = 5,
    Processing = 6,
    Approved = 7,
    Rejected = 8,
    Refunded = 9,
    Expired = 10
}

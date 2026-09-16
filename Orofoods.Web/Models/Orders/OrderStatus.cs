namespace Orofoods.Web.Models.Orders;

public enum OrderStatus
{
    Draft,
    Received,
    UnderReview,
    Approved,
    Picking,
    Invoiced,
    OutForDelivery,
    Delivered,
    Cancelled
}

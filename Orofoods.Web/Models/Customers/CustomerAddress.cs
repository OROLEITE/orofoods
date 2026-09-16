using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Customers;

public class CustomerAddress
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(60)] public string Label { get; set; } = "Principal";
    [MaxLength(12)] public string ZipCode { get; set; } = "";
    [MaxLength(180)] public string Street { get; set; } = "";
    [MaxLength(20)] public string Number { get; set; } = "";
    [MaxLength(80)] public string Complement { get; set; } = "";
    [MaxLength(80)] public string District { get; set; } = "";
    [MaxLength(80)] public string City { get; set; } = "";
    [MaxLength(2)] public string State { get; set; } = "SP";
    [MaxLength(40)] public string DeliveryDay { get; set; } = "A confirmar";
    public bool IsPrimary { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

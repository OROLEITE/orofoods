using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models.Customers;

public class SalesRepresentative
{
    public int Id { get; set; }
    [MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(160)] public string Email { get; set; } = "";
    [MaxLength(30)] public string Phone { get; set; } = "";
    [MaxLength(120)] public string Region { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Customer> Customers { get; set; } = [];
    public List<ApplicationUser> Users { get; set; } = [];
}

using System.ComponentModel.DataAnnotations;

namespace Orofoods.Web.Models;

public enum CustomerStatus { Pending, Approved, Blocked, Inactive }
public enum OrderStatus { Draft, Received, UnderReview, Approved, Picking, Invoiced, OutForDelivery, Delivered, Cancelled }

public class Customer
{
    public int Id { get; set; }
    [MaxLength(160)] public string LegalName { get; set; } = "";
    [MaxLength(120)] public string TradeName { get; set; } = "";
    [MaxLength(18)] public string Cnpj { get; set; } = "";
    public CustomerStatus Status { get; set; } = CustomerStatus.Pending;
    public decimal MinimumOrder { get; set; }
    public decimal CreditLimit { get; set; }
    public decimal CreditUsed { get; set; }
    [MaxLength(60)] public string PriceTableName { get; set; } = "Tabela padrão";
    [MaxLength(80)] public string PaymentTerms { get; set; } = "PIX";
    public List<CustomerAddress> Addresses { get; set; } = [];
}

public class CustomerAddress
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(60)] public string Label { get; set; } = "Principal";
    [MaxLength(180)] public string Street { get; set; } = "";
    [MaxLength(80)] public string City { get; set; } = "";
    [MaxLength(2)] public string State { get; set; } = "SP";
    [MaxLength(40)] public string DeliveryDay { get; set; } = "A confirmar";
}

public class Product
{
    public int Id { get; set; }
    [MaxLength(30)] public string Sku { get; set; } = "";
    [MaxLength(140)] public string Name { get; set; } = "";
    [MaxLength(60)] public string Category { get; set; } = "";
    [MaxLength(200)] public string Description { get; set; } = "";
    [MaxLength(60)] public string UnitDescription { get; set; } = "";
    public int UnitsPerCase { get; set; }
    public int MinimumCases { get; set; } = 1;
    public decimal BasePrice { get; set; }
    public bool IsAvailable { get; set; } = true;
    public int? SubstituteProductId { get; set; }
    public Product? SubstituteProduct { get; set; }
    [MaxLength(12)] public string Accent { get; set; } = "#d99624";
}

public class CustomerPrice
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int ProductId { get; set; }
    public decimal Price { get; set; }
}

public class Order
{
    public int Id { get; set; }
    [MaxLength(30)] public string Number { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? DeliveryAddressId { get; set; }
    public CustomerAddress? DeliveryAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? RequestedDeliveryDate { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    [MaxLength(40)] public string PaymentMethod { get; set; } = "PIX";
    [MaxLength(500)] public string Notes { get; set; } = "";
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

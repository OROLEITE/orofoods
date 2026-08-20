using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models;

namespace Orofoods.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        if (await db.Products.AnyAsync()) return;

        var brioche = new Product { Sku = "PAO-001", Name = "Pão Brioche 75 g", Category = "Pães", Description = "Macio, dourado e padronizado para operações de alto giro.", UnitDescription = "Caixa com 30 unidades", UnitsPerCase = 30, MinimumCases = 2, BasePrice = 92.90m, Accent = "#d99624" };
        var smash = new Product { Sku = "PAO-004", Name = "Pão Smash 65 g", Category = "Pães", Description = "Diâmetro ideal para burgers smash.", UnitDescription = "Caixa com 36 unidades", UnitsPerCase = 36, MinimumCases = 2, BasePrice = 86.40m, Accent = "#c56f24" };
        var australiano = new Product { Sku = "PAO-007", Name = "Pão Australiano", Category = "Pães", Description = "Notas de cacau e mel, pronto para descongelar.", UnitDescription = "Caixa com 24 unidades", UnitsPerCase = 24, MinimumCases = 1, BasePrice = 104.80m, Accent = "#6e402c" };
        var barbecue = new Product { Sku = "MOL-012", Name = "Molho Barbecue Defumado", Category = "Molhos", Description = "Perfil equilibrado para uso profissional.", UnitDescription = "Caixa com 6 bisnagas de 1,1 kg", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 118.50m, Accent = "#8e3023" };
        var mayo = new Product { Sku = "MOL-018", Name = "Maionese Especial", Category = "Molhos", Description = "Cremosidade consistente e alto rendimento.", UnitDescription = "Caixa com 6 bisnagas de 1 kg", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 109.90m, Accent = "#e4c068" };
        var cheddar = new Product { Sku = "MOL-021", Name = "Molho Cheddar", Category = "Molhos", Description = "Cheddar cremoso para finalização.", UnitDescription = "Caixa com 6 bisnagas de 1,01 kg", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 139.80m, Accent = "#ef9d27" };
        var ketchup = new Product { Sku = "CON-031", Name = "Ketchup Food Service", Category = "Condimentos", Description = "Sabor clássico, embalagem de alto rendimento.", UnitDescription = "Caixa com 6 sachês de 2 kg", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 96m, Accent = "#c9382d" };
        var garlic = new Product { Sku = "MOL-024", Name = "Molho de Alho", Category = "Molhos", Description = "Tempero marcante e textura cremosa.", UnitDescription = "Caixa com 6 bisnagas de 1 kg", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 114.20m, IsAvailable = false, SubstituteProduct = mayo, Accent = "#d8cab1" };
        db.Products.AddRange(brioche, smash, australiano, barbecue, mayo, cheddar, ketchup, garlic);

        var customer = new Customer { LegalName = "Burger da Vila Alimentos Ltda.", TradeName = "Burger da Vila", Cnpj = "12.345.678/0001-90", Status = CustomerStatus.Approved, MinimumOrder = 650m, CreditLimit = 8000m, CreditUsed = 2140.50m, PriceTableName = "Hamburgueria Parceira", PaymentTerms = "PIX ou 14 dias" };
        customer.Addresses.Add(new CustomerAddress { Label = "Loja Centro", Street = "Rua das Palmeiras, 142 — Centro", City = "Campinas", State = "SP", DeliveryDay = "Quintas-feiras" });
        customer.Addresses.Add(new CustomerAddress { Label = "Unidade Cambuí", Street = "Av. Norte-Sul, 880 — Cambuí", City = "Campinas", State = "SP", DeliveryDay = "Sextas-feiras" });
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        db.CustomerPrices.AddRange(
            new CustomerPrice { CustomerId = customer.Id, ProductId = brioche.Id, Price = 84.90m },
            new CustomerPrice { CustomerId = customer.Id, ProductId = smash.Id, Price = 79.90m },
            new CustomerPrice { CustomerId = customer.Id, ProductId = barbecue.Id, Price = 111.50m },
            new CustomerPrice { CustomerId = customer.Id, ProductId = mayo.Id, Price = 102.90m });
        var order = new Order { Number = "ORO-2026-001245", CustomerId = customer.Id, DeliveryAddressId = customer.Addresses[0].Id, CreatedAt = DateTime.Now.AddDays(-7), RequestedDeliveryDate = DateTime.Today.AddDays(-4), Status = OrderStatus.Delivered, PaymentMethod = "Prazo 14 dias" };
        order.Items.AddRange([
            new OrderItem { ProductId = brioche.Id, Quantity = 6, UnitPrice = 84.90m },
            new OrderItem { ProductId = smash.Id, Quantity = 3, UnitPrice = 79.90m },
            new OrderItem { ProductId = barbecue.Id, Quantity = 1, UnitPrice = 111.50m },
            new OrderItem { ProductId = mayo.Id, Quantity = 1, UnitPrice = 102.90m }
        ]);
        order.Total = order.Items.Sum(x => x.Quantity * x.UnitPrice);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Administrador", "Vendedor", "Cliente" })
            if (!await roleManager.RoleExistsAsync(role)) await roleManager.CreateAsync(new IdentityRole(role));
    }
}

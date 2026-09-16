using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Orofoods.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var role in new[] { "Administrador", "Vendedor", "Cliente" })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureProductCategoriesAsync(db);

        if (!await db.PriceTables.AnyAsync())
        {
            db.PriceTables.AddRange(
                new PriceTable { Name = "Tabela Padrao", IsActive = true },
                new PriceTable { Name = "Tabela Hamburgueria", IsActive = true },
                new PriceTable { Name = "Tabela Promocional", IsActive = true });
            await db.SaveChangesAsync();
        }

        if (!await db.PaymentTerms.AnyAsync())
        {
            db.PaymentTerms.AddRange(
                new PaymentTerm { Name = "PIX", SortOrder = 1, IsActive = true },
                new PaymentTerm { Name = "7 dias", SortOrder = 2, IsActive = true },
                new PaymentTerm { Name = "14 dias", SortOrder = 3, IsActive = true });
            await db.SaveChangesAsync();
        }

        var salesRepresentative = await db.SalesRepresentatives.FirstOrDefaultAsync(x => x.Email == "vendedor@orofoods.local");
        if (salesRepresentative is null)
        {
            salesRepresentative = new SalesRepresentative
            {
                Name = "Consultor Orofoods",
                Email = "vendedor@orofoods.local",
                Phone = "(19) 99999-1111",
                Region = "Campinas/SP",
                IsActive = true
            };
            db.SalesRepresentatives.Add(salesRepresentative);
            await db.SaveChangesAsync();
        }
        else if (string.IsNullOrWhiteSpace(salesRepresentative.Region))
        {
            salesRepresentative.Region = "Campinas/SP";
            await db.SaveChangesAsync();
        }

        var breadCategory = await db.ProductCategories.SingleAsync(x => x.Slug == "paes");
        var frozenCategory = await db.ProductCategories.SingleAsync(x => x.Slug == "congelados");
        var sauceCategory = await db.ProductCategories.SingleAsync(x => x.Slug == "molhos");
        var categoryProducts = new[]
        {
            new Product { Sku = "PAO-001", Name = "Pão Brioche 75 g", ProductCategoryId = breadCategory.Id, Brand = "Orofoods", Description = "Macio e padronizado para alto giro.", Unit = "caixa", UnitsPerCase = 30, MinimumCases = 2, BasePrice = 92.90m, IsAvailable = true, IsActive = true, Accent = "#d99624" },
            new Product { Sku = "PAO-004", Name = "Pão Smash 65 g", ProductCategoryId = breadCategory.Id, Brand = "Orofoods", Description = "Ideal para smash burgers.", Unit = "caixa", UnitsPerCase = 36, MinimumCases = 2, BasePrice = 86.40m, IsAvailable = true, IsActive = true, Accent = "#c56f24" },
            new Product { Sku = "MOL-012", Name = "Molho Barbecue Defumado", ProductCategoryId = sauceCategory.Id, Brand = "Orofoods", Description = "Perfil equilibrado para food service.", Unit = "caixa", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 118.50m, IsAvailable = true, IsActive = true, Accent = "#8e3023" },
            new Product { Sku = "MOL-018", Name = "Maionese Especial", ProductCategoryId = sauceCategory.Id, Brand = "Orofoods", Description = "Cremosidade consistente.", Unit = "caixa", UnitsPerCase = 6, MinimumCases = 1, BasePrice = 109.90m, IsAvailable = true, IsActive = true, Accent = "#e4c068" }
        };

        foreach (var product in categoryProducts)
        {
            if (!await db.Products.AnyAsync(x => x.Sku == product.Sku))
            {
                db.Products.Add(product);
            }
        }

        // Keep legacy demo data available to historical orders, but out of every active catalog.
        var legacyExampleSkus = categoryProducts.Select(product => product.Sku).ToArray();
        var existingExamples = await db.Products
            .Where(product => legacyExampleSkus.Contains(product.Sku))
            .ToListAsync();
        foreach (var existingExample in existingExamples)
        {
            existingExample.IsActive = false;
            existingExample.IsAvailable = false;
        }

        var bimboProducts = new[]
        {
            new Product { Sku = "BIM-001", Name = "Pão de Hambúrguer Brioche Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Pão brioche congelado, macio e padronizado para hamburguerias.", Unit = "caixa", UnitsPerPackage = 12, UnitsPerCase = 72, MinimumCases = 1, BasePrice = 128.90m, IsAvailable = true, IsActive = true, IsFeatured = true, Accent = "#0d4a98" },
            new Product { Sku = "BIM-002", Name = "Pão de Hambúrguer com Gergelim Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Pão com gergelim congelado para montagem com acabamento clássico.", Unit = "caixa", UnitsPerPackage = 12, UnitsPerCase = 72, MinimumCases = 1, BasePrice = 132.90m, IsAvailable = true, IsActive = true, Accent = "#174d91" },
            new Product { Sku = "BIM-003", Name = "Mini Pão de Hambúrguer Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Mini pão congelado para sliders, eventos e porções especiais.", Unit = "caixa", UnitsPerPackage = 18, UnitsPerCase = 108, MinimumCases = 1, BasePrice = 119.90m, IsAvailable = true, IsActive = true, Accent = "#154a91" },
            new Product { Sku = "BIM-004", Name = "Pão de Hambúrguer Smart Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Pão de 75 g, fatiado e congelado para operações de alto giro.", Unit = "caixa", UnitsPerPackage = 10, UnitsPerCase = 180, MinimumCases = 1, BasePrice = 134.90m, IsAvailable = true, IsActive = true, IsFeatured = true, Accent = "#0d4a98" },
            new Product { Sku = "BIM-005", Name = "Pão de Hambúrguer Tradicional Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Pão tradicional congelado para uma montagem consistente no food service.", Unit = "caixa", UnitsPerPackage = 10, UnitsPerCase = 120, MinimumCases = 1, BasePrice = 124.90m, IsAvailable = true, IsActive = true, Accent = "#174d91" },
            new Product { Sku = "BIM-006", Name = "Pão de Hambúrguer Tradicional com Gergelim", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Versão tradicional com gergelim para acabamento clássico em hambúrgueres.", Unit = "caixa", UnitsPerPackage = 10, UnitsPerCase = 120, MinimumCases = 1, BasePrice = 129.90m, IsAvailable = true, IsActive = true, Accent = "#174d91" },
            new Product { Sku = "BIM-007", Name = "Pão Clássico Brioche Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Pão brioche congelado, macio e dourado, indicado para hambúrgueres premium.", Unit = "caixa", UnitsPerPackage = 10, UnitsPerCase = 120, MinimumCases = 1, BasePrice = 139.90m, IsAvailable = true, IsActive = true, Accent = "#0d4a98" },
            new Product { Sku = "BIM-008", Name = "Pão de Hambúrguer Australiano Congelado", ProductCategoryId = frozenCategory.Id, Brand = "BIMBO", Description = "Pão australiano congelado, com sabor marcante para receitas especiais.", Unit = "caixa", UnitsPerPackage = 10, UnitsPerCase = 120, MinimumCases = 1, BasePrice = 149.90m, IsAvailable = true, IsActive = true, IsFeatured = true, Accent = "#47382b" }
        };
        foreach (var product in bimboProducts)
        {
            var existingProduct = await db.Products.SingleOrDefaultAsync(x => x.Sku == product.Sku);
            if (existingProduct is null)
            {
                db.Products.Add(product);
                continue;
            }

            existingProduct.Name = product.Name;
            existingProduct.Description = product.Description;
            existingProduct.ProductCategoryId = product.ProductCategoryId;
            existingProduct.Brand = product.Brand;
            existingProduct.Unit = product.Unit;
            existingProduct.UnitsPerPackage = product.UnitsPerPackage;
            existingProduct.UnitsPerCase = product.UnitsPerCase;
            existingProduct.MinimumCases = product.MinimumCases;
            existingProduct.BasePrice = product.BasePrice;
            existingProduct.IsAvailable = product.IsAvailable;
            existingProduct.IsActive = product.IsActive;
            existingProduct.IsFeatured = product.IsFeatured;
            existingProduct.Accent = product.Accent;
        }

        await db.SaveChangesAsync();

        var bimboImages = new Dictionary<string, string>
        {
            ["BIM-001"] = "/images/products/bimbo-brioche-loose.png",
            ["BIM-002"] = "/images/products/bimbo-gergelim-loose.png",
            ["BIM-003"] = "/images/products/bimbo-brioche-loose.png",
            ["BIM-004"] = "/images/products/bimbo-smart-loose.png",
            ["BIM-005"] = "/images/products/bimbo-gergelim-loose.png",
            ["BIM-006"] = "/images/products/bimbo-gergelim-loose.png",
            ["BIM-007"] = "/images/products/bimbo-brioche-loose.png",
            ["BIM-008"] = "/images/products/bimbo-australiano-loose.png"
        };
        foreach (var item in bimboImages)
        {
            var product = await db.Products.SingleAsync(x => x.Sku == item.Key);
            var existingImage = await db.ProductImages
                .Where(image => image.ProductId == product.Id)
                .OrderBy(image => image.SortOrder)
                .FirstOrDefaultAsync();
            if (existingImage is null)
                db.ProductImages.Add(new ProductImage { ProductId = product.Id, Url = item.Value, AltText = product.Name, IsPrimary = true, SortOrder = 0 });
            else
            {
                existingImage.Url = item.Value;
                existingImage.AltText = product.Name;
                existingImage.IsPrimary = true;
                existingImage.SortOrder = 0;
            }
        }
        await db.SaveChangesAsync();

        var inventoryProductIds = await db.ProductInventories
            .Select(inventory => inventory.ProductId)
            .ToListAsync();
        var productsWithoutInventory = await db.Products
            .Where(product => product.IsActive && !inventoryProductIds.Contains(product.Id))
            .Select(product => product.Id)
            .ToListAsync();
        db.ProductInventories.AddRange(productsWithoutInventory.Select(productId => new ProductInventory
        {
            ProductId = productId,
            QuantityOnHand = 100
        }));
        await db.SaveChangesAsync();

        var burgerPriceTable = await db.PriceTables.SingleAsync(x => x.Name == "Tabela Hamburgueria");
        var pix = await db.PaymentTerms.SingleAsync(x => x.Name == "PIX");
        var fourteenDays = await db.PaymentTerms.SingleAsync(x => x.Name == "14 dias");

        var approvedCustomer = await db.Customers
            .Include(x => x.Addresses)
            .Include(x => x.CustomerPaymentTerms)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Cnpj == "12.345.678/0001-90");
        if (approvedCustomer is null)
        {
            approvedCustomer = new Customer
            {
                LegalName = "Burger da Vila Alimentos Ltda.",
                TradeName = "Burger da Vila",
                Cnpj = "12.345.678/0001-90",
                Status = CustomerStatus.Approved,
                ResponsibleName = "Patricia Souza",
                Email = "compras@burgerdavila.com",
                Phone = "(19) 3322-1100",
                WhatsApp = "(19) 99999-2200",
                MinimumOrder = 100m,
                CreditLimit = 8000m,
                CreditUsed = 2140.50m,
                PriceTableId = burgerPriceTable.Id,
                SalesRepresentativeId = salesRepresentative.Id,
                ApprovedAt = DateTime.UtcNow,
                IsActive = true
            };
            approvedCustomer.Addresses.Add(new CustomerAddress { Label = "Loja Centro", Street = "Rua das Palmeiras", Number = "142", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-000", DeliveryDay = "Quintas-feiras", IsPrimary = true, IsActive = true });
            approvedCustomer.Addresses.Add(new CustomerAddress { Label = "Unidade Cambui", Street = "Av. Norte-Sul", Number = "880", District = "Cambuí", City = "Campinas", State = "SP", ZipCode = "13000-100", DeliveryDay = "Sextas-feiras", IsPrimary = false, IsActive = true });
            approvedCustomer.CustomerPaymentTerms.Add(new CustomerPaymentTerm { PaymentTermId = pix.Id, IsActive = true });
            approvedCustomer.CustomerPaymentTerms.Add(new CustomerPaymentTerm { PaymentTermId = fourteenDays.Id, IsActive = true });
            db.Customers.Add(approvedCustomer);
            await db.SaveChangesAsync();
        }

        var pendingCustomer = await db.Customers.FirstOrDefaultAsync(x => x.Cnpj == "98.765.432/0001-10");
        if (pendingCustomer is null)
        {
            pendingCustomer = new Customer
            {
                LegalName = "Lanches da Praca Ltda.",
                TradeName = "Lanches da Praca",
                Cnpj = "98.765.432/0001-10",
                Status = CustomerStatus.Pending,
                ResponsibleName = "Marcos Lima",
                Email = "cadastro@lanchesdapraca.com",
                Phone = "(19) 3444-5566",
                WhatsApp = "(19) 98888-7766",
                MinimumOrder = 100m,
                CreditLimit = 0m,
                CreditUsed = 0m,
                SalesRepresentativeId = salesRepresentative.Id,
                IsActive = true
            };
            pendingCustomer.Addresses.Add(new CustomerAddress { Label = "Principal", Street = "Rua do Mercado", Number = "45", District = "Centro", City = "Campinas", State = "SP", ZipCode = "13000-200", DeliveryDay = "A definir", IsPrimary = true, IsActive = true });
            db.Customers.Add(pendingCustomer);
            await db.SaveChangesAsync();
        }

        var priceTargets = new Dictionary<string, decimal>
        {
            ["PAO-001"] = 84.90m,
            ["PAO-004"] = 79.90m,
            ["MOL-012"] = 111.50m,
            ["MOL-018"] = 102.90m
        };

        foreach (var target in priceTargets)
        {
            var product = await db.Products.SingleAsync(x => x.Sku == target.Key);
            if (!await db.PriceTableItems.AnyAsync(x => x.PriceTableId == burgerPriceTable.Id && x.ProductId == product.Id))
            {
                db.PriceTableItems.Add(new PriceTableItem
                {
                    PriceTableId = burgerPriceTable.Id,
                    ProductId = product.Id,
                    Price = target.Value
                });
            }
        }

        await db.SaveChangesAsync();

        await EnsureUserAsync(userManager, "admin@orofoods.local", "Admin123!", "Administrador", null, null);
        await EnsureUserAsync(userManager, "vendedor@orofoods.local", "Vendedor123!", "Vendedor", null, salesRepresentative.Id);
        await EnsureUserAsync(userManager, "compras@burgerdavila.com", "Cliente123!", "Cliente", approvedCustomer.Id, salesRepresentative.Id);
        await EnsureUserAsync(userManager, "cadastro@lanchesdapraca.com", "Cliente123!", "Cliente", pendingCustomer.Id, salesRepresentative.Id);
        var buyer = await userManager.FindByEmailAsync("compras@burgerdavila.com")
            ?? throw new InvalidOperationException("Seed buyer not found.");

        if (!await db.Orders.AnyAsync(x => x.CustomerId == approvedCustomer.Id))
        {
            var brioche = await db.Products.SingleAsync(x => x.Sku == "PAO-001");
            var smash = await db.Products.SingleAsync(x => x.Sku == "PAO-004");
            var barbecue = await db.Products.SingleAsync(x => x.Sku == "MOL-012");
            var mayo = await db.Products.SingleAsync(x => x.Sku == "MOL-018");

            var order = new Order
            {
                Number = "ORO-2026-001245",
                CustomerId = approvedCustomer.Id,
                CreatedByUserId = buyer.Id,
                DeliveryAddressId = approvedCustomer.Addresses.First().Id,
                RequestedDeliveryDate = DateTime.UtcNow.Date.AddDays(-4),
                CreatedAt = DateTime.UtcNow.AddDays(-7),
                Status = OrderStatus.Delivered,
                PaymentMethod = "14 dias",
                Freight = 0m
            };

            order.Items.AddRange(
            [
                new OrderItem { ProductId = brioche.Id, ProductNameSnapshot = brioche.Name, SkuSnapshot = brioche.Sku, Quantity = 6, UnitPrice = 84.90m, Subtotal = 509.40m },
                new OrderItem { ProductId = smash.Id, ProductNameSnapshot = smash.Name, SkuSnapshot = smash.Sku, Quantity = 3, UnitPrice = 79.90m, Subtotal = 239.70m },
                new OrderItem { ProductId = barbecue.Id, ProductNameSnapshot = barbecue.Name, SkuSnapshot = barbecue.Sku, Quantity = 1, UnitPrice = 111.50m, Subtotal = 111.50m },
                new OrderItem { ProductId = mayo.Id, ProductNameSnapshot = mayo.Name, SkuSnapshot = mayo.Sku, Quantity = 1, UnitPrice = 102.90m, Subtotal = 102.90m }
            ]);
            order.Subtotal = order.Items.Sum(x => x.Subtotal);
            order.Total = order.Subtotal;
            order.StatusHistory.Add(new OrderStatusHistory
            {
                Status = order.Status,
                ChangedAt = order.CreatedAt,
                ChangedByUserId = buyer.Id
            });
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string role,
        int? customerId,
        int? salesRepresentativeId)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                CustomerId = customerId,
                SalesRepresentativeId = salesRepresentativeId,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    public static async Task EnsureProductCategoriesAsync(ApplicationDbContext db)
    {
        var categories = new[]
        {
            new ProductCategory { Name = "Paes", Slug = "paes", SortOrder = 1, IsActive = true },
            new ProductCategory { Name = "Molhos", Slug = "molhos", SortOrder = 2, IsActive = true },
            new ProductCategory { Name = "Condimentos", Slug = "condimentos", SortOrder = 3, IsActive = true },
            new ProductCategory { Name = "Congelados", Slug = "congelados", SortOrder = 4, IsActive = true },
            new ProductCategory { Name = "Queijos", Slug = "queijos", SortOrder = 5, IsActive = true },
            new ProductCategory { Name = "Complementos", Slug = "complementos", SortOrder = 6, IsActive = true }
        };

        var existingSlugs = await db.ProductCategories
            .Select(x => x.Slug)
            .ToListAsync();
        db.ProductCategories.AddRange(categories.Where(x => !existingSlugs.Contains(x.Slug)));
        await db.SaveChangesAsync();
    }
}

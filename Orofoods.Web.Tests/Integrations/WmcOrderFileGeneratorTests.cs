using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Models.Catalog;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Orders;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Integrations;

public class WmcOrderFileGeneratorTests
{
    [Fact]
    public void Writes_every_order_record_field_at_its_neogrid_v32_position()
    {
        const string customerCnpj = "03932292000199";
        var order = new Order
        {
            Number = "ORO-2026-000006",
            CreatedAt = new DateTime(2026, 10, 13, 12, 59, 0, DateTimeKind.Utc),
            RequestedDeliveryDate = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { Cnpj = "03.932.292/0001-99" },
            PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 },
            Subtotal = 19.90m,
            Total = 19.90m,
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "243680", Unit = "UN" },
                    ProductNameSnapshot = "Produto 243680 Homologacao",
                    Quantity = 1,
                    UnitPrice = 19.90m,
                    Subtotal = 19.90m
                }
            ]
        };

        var result = WmcTestOrderFileGenerator.Create().Build(order);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var records = result.Content!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal([315, 45, 122, 330, 122], records.Select(record => record.Length));

        AssertFields(records[0],
            (0, 3, "019"), (3, 5, "  001"),
            (8, 20, "2026000006".PadRight(20)), (28, 20, "2026000006".PadRight(20)),
            (48, 12, "202610131259"), (60, 12, "202610150000"), (72, 12, "202610150000"),
            (84, 15, Spaces(15)), (99, 15, Spaces(15)),
            (114, 13, Spaces(13)), (127, 13, Spaces(13)), (140, 13, Spaces(13)), (153, 13, Spaces(13)),
            (166, 14, WmcTestOrderFileGenerator.HomologatedSupplierCnpj), (180, 14, customerCnpj),
            (194, 14, customerCnpj), (208, 14, customerCnpj),
            (222, 3, Spaces(3)), (225, 14, "00000000000000"), (239, 30, Spaces(30)),
            (269, 3, "CIF"), (272, 3, "000"), (275, 40, Spaces(40)));

        AssertFields(records[1],
            (0, 2, "02"), (2, 3, "1  "), (5, 3, "5  "), (8, 3, "1  "),
            (11, 3, "CD "), (14, 3, "000"), (17, 8, "20261013"),
            (25, 15, "000000000001990"), (40, 5, "10000"));

        AssertFields(records[2],
            (0, 2, "03"), (2, 5, "00000"), (7, 15, "000000000000000"),
            (22, 5, "00000"), (27, 15, "000000000000000"),
            (42, 5, "00000"), (47, 15, "000000000000000"),
            (62, 5, "00000"), (67, 15, "000000000000000"),
            (82, 5, "00000"), (87, 15, "000000000000000"),
            (102, 5, "00000"), (107, 15, "000000000000000"));

        AssertFields(records[3],
            (0, 2, "04"), (2, 4, "0001"), (6, 5, "00000"), (11, 3, Spaces(3)),
            (14, 3, Spaces(3)), (17, 14, "243680".PadRight(14)),
            (31, 40, "PRODUTO 243680 HOMOLOGACAO".PadRight(40)), (71, 20, Spaces(20)),
            (91, 3, "UN "), (94, 5, "00000"), (99, 15, "000000000000100"),
            (114, 15, "000000000000000"), (129, 15, "000000000000000"),
            (144, 3, Spaces(3)), (147, 5, "00000"),
            (152, 15, "000000000001990"), (167, 15, "000000000001990"),
            (182, 15, "000000000001990"), (197, 15, "000000000001990"),
            (212, 5, "00001"), (217, 3, "UN "),
            (220, 15, "000000000000000"), (235, 5, "00000"),
            (240, 15, "000000000000000"), (255, 5, "00000"),
            (260, 15, "000000000000000"), (275, 15, "000000000000000"),
            (290, 15, "000000000000000"), (305, 7, "0000000"),
            (312, 8, "00000000"), (320, 10, Spaces(10)));

        AssertFields(records[4],
            (0, 2, "09"), (2, 15, "000000000001990"), (17, 15, "000000000000000"),
            (32, 15, "000000000000000"), (47, 15, "000000000000000"),
            (62, 15, "000000000000000"), (77, 15, "000000000000000"),
            (92, 15, "000000000000000"), (107, 15, "000000000001990"));
    }

    [Fact]
    public void Requires_a_valid_customer_cnpj_without_requiring_customer_wmc_code()
    {
        var order = new Order
        {
            Number = "ORO-2026-000780",
            CreatedAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { Cnpj = "03932292000199" },
            PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 },
            Subtotal = 19.90m,
            Total = 19.90m,
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "243680", Unit = "UN" },
                    ProductNameSnapshot = "Produto homologacao",
                    Quantity = 1,
                    UnitPrice = 19.90m,
                    Subtotal = 19.90m
                }
            ]
        };

        var result = WmcTestOrderFileGenerator.Create().Build(order);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var header = result.Content!.Split(Environment.NewLine)[0];
        Assert.Equal("03932292000199", header.Substring(180, 14));
        Assert.Equal("03932292000199", header.Substring(194, 14));
        Assert.Equal("03932292000199", header.Substring(208, 14));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12.345.678/0001-9")]
    [InlineData("12.345.678/0001-999")]
    public void Rejects_customer_cnpj_that_does_not_normalize_to_14_digits(string? cnpj)
    {
        var order = new Order
        {
            Customer = new Customer { Cnpj = cnpj! },
            PaymentTerm = new PaymentTerm { Code = "PIX", DaysUntilDue = 0 },
            Items = []
        };

        Assert.Contains("WMC_CUSTOMER_CNPJ_INVALID", WmcTestOrderFileGenerator.Create().Validate(order));
    }

    [Fact]
    public void Builds_a_fixed_width_homologation_file_for_a_mapped_order()
    {
        var order = new Order
        {
            Number = "ORO-2026-000777",
            CreatedAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { WmcCode = "107072", Cnpj = "03.932.292/0001-99" },
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "610601552", Unit = "caixa" },
                    ProductNameSnapshot = "Pao brioche congelado",
                    Quantity = 12,
                    UnitPrice = 63.20m,
                    Subtotal = 758.40m
                }
            ]
        };

        order.Total = 758.40m;
        order.PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 };
        var result = WmcTestOrderFileGenerator.Create().Build(order);
        var lines = result.Content!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.True(result.Succeeded);
        Assert.Equal([315, 45, 122, 330, 122], lines.Select(line => line.Length));
        Assert.StartsWith("019", lines[0]);
        Assert.Contains("00020260830", lines[1]);
        Assert.Contains("610601552", lines[3]);
        Assert.StartsWith("090", lines[4]);
    }

    [Fact]
    public void Rejects_order_when_a_product_mapping_is_missing()
    {
        var order = new Order
        {
            Customer = new Customer { Cnpj = "03.932.292/0001-99" },
            PaymentTerm = new PaymentTerm { Code = "PIX", DaysUntilDue = 0 },
            Items = [new OrderItem { Product = new Product(), ProductNameSnapshot = "Pao", Quantity = 1 }]
        };

        var result = WmcTestOrderFileGenerator.Create().Build(order);

        Assert.False(result.Succeeded);
        Assert.Null(result.Content);
        Assert.Contains(result.Errors, error => error.Contains("produto", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Exports_card_on_delivery_as_due_today_without_changing_the_payment_term()
    {
        var order = new Order
        {
            Number = "ORO-2026-000778",
            CreatedAt = new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { WmcCode = "107072", Cnpj = "03.932.292/0001-99" },
            PaymentTerm = new PaymentTerm { Code = "CARD_ON_DELIVERY", Name = "Cartão na entrega", DaysUntilDue = 14 },
            Total = 63.20m,
            Items = [new OrderItem { Product = new Product { WmcCode = "610601552", Unit = "caixa" }, ProductNameSnapshot = "Pao", Quantity = 1, UnitPrice = 63.20m, Subtotal = 63.20m }]
        };

        var result = WmcTestOrderFileGenerator.Create().Build(order);
        var paymentRecord = result.Content?.Split(Environment.NewLine)[1];

        Assert.True(result.Succeeded);
        Assert.Equal(45, paymentRecord?.Length);
        Assert.Contains("00020260830", paymentRecord);
        Assert.Equal(14, order.PaymentTerm.DaysUntilDue);
    }

    [Fact]
    public void Uses_the_configured_supplier_cnpj_in_record_019()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Orofoods.Web"));
        using var appSettings = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(projectPath, "appsettings.json")));
        var configuredSupplierCnpj = appSettings.RootElement
            .GetProperty("WmcOrderFile")
            .GetProperty("SupplierCnpj")
            .GetString()!;

        Assert.Equal("51735297000174", configuredSupplierCnpj);
        var order = new Order
        {
            Number = "ORO-2026-000779",
            CreatedAt = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
            Customer = new Customer { WmcCode = "107072", Cnpj = "03.932.292/0001-99" },
            PaymentTerm = new PaymentTerm { Code = "PIX", Name = "PIX", DaysUntilDue = 0 },
            Subtotal = 19.90m,
            Total = 19.90m,
            Items =
            [
                new OrderItem
                {
                    Product = new Product { WmcCode = "243680", Unit = "UN" },
                    ProductNameSnapshot = "Produto homologacao",
                    Quantity = 1,
                    UnitPrice = 19.90m,
                    Subtotal = 19.90m
                }
            ]
        };

        var result = WmcTestOrderFileGenerator.Create(configuredSupplierCnpj).Build(order);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(configuredSupplierCnpj, result.Content![166..180]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2947522200014")]
    [InlineData("2947522200014X")]
    [InlineData("29.475.222/0001-42")]
    public void Rejects_invalid_supplier_cnpj_configuration(string? supplierCnpj)
    {
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(
            () => WmcTestOrderFileGenerator.Create(supplierCnpj));
    }

    private static string Spaces(int length) => new(' ', length);

    private static void AssertFields(string record, params (int Start, int Length, string Expected)[] fields)
    {
        foreach (var (start, length, expected) in fields)
        {
            Assert.Equal(expected, record.Substring(start, length));
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Orofoods.Web.Models.Customers;
using Orofoods.Web.Models.Pricing;
using Orofoods.Web.Services.Customers;
using Orofoods.Web.Tests.Infrastructure;

namespace Orofoods.Web.Tests.Services;

public class CustomerApprovalServiceTests
{
    [Fact]
    public async Task Approving_customer_assigns_commercial_configuration()
    {
        await using var db = await TestDbContextFactory.CreateAsync();
        var paymentTerm = new PaymentTerm { Name = "14 dias", SortOrder = 1, IsActive = true };
        var priceTable = new PriceTable { Name = "Tabela Hamburgueria", IsActive = true };
        var salesRepresentative = new SalesRepresentative { Name = "Consultor", Email = "consultor@orofoods.local", Phone = "(19) 99999-0000", IsActive = true };
        var customer = new Customer
        {
            LegalName = "Burger House Ltda",
            TradeName = "Burger House",
            Cnpj = "12.345.678/0001-99",
            Status = CustomerStatus.Pending,
            IsActive = true
        };

        db.AddRange(paymentTerm, priceTable, salesRepresentative, customer);
        await db.SaveChangesAsync();

        var sut = new CustomerApprovalService(db);
        await sut.ApplyAsync(new CustomerApprovalRequest
        {
            CustomerId = customer.Id,
            Status = CustomerStatus.Approved,
            MinimumOrder = 300m,
            CreditLimit = 5000m,
            PriceTableId = priceTable.Id,
            SalesRepresentativeId = salesRepresentative.Id,
            WmcCode = "107072",
            PaymentTermIds = [paymentTerm.Id]
        });

        var updatedCustomer = await db.Customers
            .Include(x => x.CustomerPaymentTerms)
            .SingleAsync(x => x.Id == customer.Id);

        Assert.Equal(CustomerStatus.Approved, updatedCustomer.Status);
        Assert.NotNull(updatedCustomer.ApprovedAt);
        Assert.Equal(300m, updatedCustomer.MinimumOrder);
        Assert.Equal(5000m, updatedCustomer.CreditLimit);
        Assert.Equal(priceTable.Id, updatedCustomer.PriceTableId);
        Assert.Equal(salesRepresentative.Id, updatedCustomer.SalesRepresentativeId);
        Assert.Equal("107072", updatedCustomer.WmcCode);
        Assert.Single(updatedCustomer.CustomerPaymentTerms);
    }
}

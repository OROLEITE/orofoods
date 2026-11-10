using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Integrations.Erp.Wmc;
using Orofoods.Web.Services.Customers;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class WmcTestPaymentEligibility
{
    public static IPaymentEligibilityService Create(
        ApplicationDbContext db,
        WmcCustomerPaymentCondition? condition = null,
        bool failRead = false) =>
        new PaymentEligibilityService(db, Options.Create(new PaymentEligibilityOptions()), new TestReader(condition, failRead));

    private sealed class TestReader(WmcCustomerPaymentCondition? condition, bool failRead) : IWmcCustomerPaymentConditionReader
    {
        public Task<WmcCustomerPaymentCondition?> GetForCustomerAsync(string customerCode, CancellationToken cancellationToken = default) =>
            failRead
                ? Task.FromException<WmcCustomerPaymentCondition?>(new InvalidOperationException("Firebird indisponível."))
                : Task.FromResult(condition);
    }
}

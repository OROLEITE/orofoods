using Microsoft.AspNetCore.Http;

namespace Orofoods.Web.Services.Identity;

public sealed class AdminCustomerContextService
{
    private const string CustomerIdKey = "AdminCustomerId";

    public int? GetSelectedCustomerId(ISession session) => session.GetInt32(CustomerIdKey);

    public void SetSelectedCustomerId(ISession session, int customerId) => session.SetInt32(CustomerIdKey, customerId);

    public void Clear(ISession session) => session.Remove(CustomerIdKey);
}

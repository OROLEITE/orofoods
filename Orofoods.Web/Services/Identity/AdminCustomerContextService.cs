using Microsoft.AspNetCore.Http;

namespace Orofoods.Web.Services.Identity;

public sealed class AdminCustomerContextService
{
    private const string CustomerIdKey = "AdminCustomerId";
    private const string AdministratorUserIdKey = "AdminCustomerSelectedBy";

    public int? GetSelectedCustomerId(ISession session, string? administratorUserId)
    {
        if (string.IsNullOrWhiteSpace(administratorUserId)
            || !string.Equals(session.GetString(AdministratorUserIdKey), administratorUserId, StringComparison.Ordinal))
        {
            return null;
        }

        return session.GetInt32(CustomerIdKey);
    }

    public void SetSelectedCustomerId(ISession session, string administratorUserId, int customerId)
    {
        session.SetString(AdministratorUserIdKey, administratorUserId);
        session.SetInt32(CustomerIdKey, customerId);
    }

    public void Clear(ISession session, string? administratorUserId = null)
    {
        if (administratorUserId is not null
            && !string.Equals(session.GetString(AdministratorUserIdKey), administratorUserId, StringComparison.Ordinal))
        {
            return;
        }

        session.Remove(AdministratorUserIdKey);
        session.Remove(CustomerIdKey);
    }
}

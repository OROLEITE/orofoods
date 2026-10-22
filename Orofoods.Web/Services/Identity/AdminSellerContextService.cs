using Microsoft.AspNetCore.Http;

namespace Orofoods.Web.Services.Identity;

public sealed class AdminSellerContextService
{
    public const string SelectedRepresentativeClaimType = "orofoods:admin-selected-sales-representative-id";
    private const string SelectedRepresentativeIdKey = "AdminSalesRepresentativeId";
    private const string AdministratorUserIdKey = "AdminSalesRepresentativeSelectedBy";

    public int? GetSelectedRepresentativeId(ISession session, string? administratorUserId)
    {
        if (string.IsNullOrWhiteSpace(administratorUserId)
            || !string.Equals(session.GetString(AdministratorUserIdKey), administratorUserId, StringComparison.Ordinal))
        {
            return null;
        }

        return session.GetInt32(SelectedRepresentativeIdKey);
    }

    public void SetSelectedRepresentativeId(ISession session, string administratorUserId, int representativeId)
    {
        session.SetString(AdministratorUserIdKey, administratorUserId);
        session.SetInt32(SelectedRepresentativeIdKey, representativeId);
    }

    public void Clear(ISession session, string? administratorUserId = null)
    {
        if (administratorUserId is not null
            && !string.Equals(session.GetString(AdministratorUserIdKey), administratorUserId, StringComparison.Ordinal))
        {
            return;
        }

        session.Remove(AdministratorUserIdKey);
        session.Remove(SelectedRepresentativeIdKey);
    }
}

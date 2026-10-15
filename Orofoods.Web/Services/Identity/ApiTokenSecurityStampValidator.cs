using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Services.Identity;

public sealed class ApiTokenSecurityStampValidator(UserManager<ApplicationUser> userManager)
{
    public const string SecurityStampClaimType = "security_stamp";

    public async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        var userId = principal is null ? null : userManager.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId)) userId = principal?.FindFirst("sub")?.Value;
        var tokenStamp = principal?.FindFirst(SecurityStampClaimType)?.Value;
        var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);

        if (user is null
            || !user.IsActive
            || user.MustChangePassword
            || string.IsNullOrWhiteSpace(tokenStamp)
            || !string.Equals(tokenStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            context.Fail("The API session has been invalidated.");
        }
    }
}

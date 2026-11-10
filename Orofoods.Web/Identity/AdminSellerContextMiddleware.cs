using System.Security.Claims;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Identity;

public sealed class AdminSellerContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AdminSellerContextService sellerContextService)
    {
        if (context.User.IsInRole(ApplicationRoles.Administrator))
        {
            var administratorUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var representativeId = sellerContextService.GetSelectedRepresentativeId(context.Session, administratorUserId);
            if (representativeId is not null)
            {
                var identity = context.User.Identity as ClaimsIdentity;
                identity?.AddClaim(new Claim(
                    AdminSellerContextService.SelectedRepresentativeClaimType,
                    representativeId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
            else if (HttpMethods.IsGet(context.Request.Method)
                && context.Request.Path.StartsWithSegments("/Vendedor"))
            {
                var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString);
                context.Response.Redirect($"/Admin/Portais/SelecionarVendedor?returnUrl={returnUrl}");
                return;
            }
        }

        await next(context);
    }
}

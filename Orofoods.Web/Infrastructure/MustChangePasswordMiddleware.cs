using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Infrastructure;

public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    private static readonly PathString ChangePasswordPath = new("/Identity/Account/ChangeTemporaryPassword");
    private static readonly PathString LogoutPath = new("/Identity/Account/Logout");

    private static readonly PathString[] StaticAssetPaths =
        [new("/css"), new("/js"), new("/lib"), new("/images"), new("/favicon.ico")];

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        // Public static assets must not be redirected, otherwise the change-password page loses its CSS/JS.
        if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            && StaticAssetPaths.Any(path => context.Request.Path.StartsWithSegments(path)))
        {
            await next(context);
            return;
        }

        var principal = context.User;
        var schemes = context.GetEndpoint()?.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .SelectMany(data => (data.AuthenticationSchemes ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (schemes.Length > 0)
        {
            principal = new System.Security.Claims.ClaimsPrincipal();
            foreach (var scheme in schemes)
            {
                var authentication = await context.AuthenticateAsync(scheme);
                if (authentication.Succeeded && authentication.Principal is not null)
                {
                    principal.AddIdentities(authentication.Principal.Identities);
                }
            }
        }

        if (principal.Identity?.IsAuthenticated != true
            || context.Request.Path.Equals(ChangePasswordPath)
            || context.Request.Path.Equals(LogoutPath))
        {
            await next(context);
            return;
        }

        var userId = userManager.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId)) userId = principal.FindFirst("sub")?.Value;
        var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);
        if (user?.MustChangePassword != true)
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        context.Response.Redirect(ChangePasswordPath);
    }
}

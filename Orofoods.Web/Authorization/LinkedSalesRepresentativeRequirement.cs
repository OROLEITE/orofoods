using Microsoft.AspNetCore.Authorization;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Authorization;

public sealed class LinkedSalesRepresentativeRequirement : IAuthorizationRequirement;

public sealed class LinkedSalesRepresentativeHandler(
    SalesRepresentativeAccessService accessService) : AuthorizationHandler<LinkedSalesRepresentativeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LinkedSalesRepresentativeRequirement requirement)
    {
        if (await accessService.HasActiveSellerAccessAsync(context.User))
        {
            context.Succeed(requirement);
        }
    }
}
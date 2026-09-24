using Microsoft.AspNetCore.Authorization;
using Orofoods.Web.Services.Identity;
using System.Security.Claims;

namespace Orofoods.Web.Authorization;

public static class OrofoodsPolicies
{
    public const string ApprovedCustomer = "ApprovedCustomer";
    public const string LinkedSalesRepresentative = "LinkedSalesRepresentative";
}

public class ApprovedCustomerRequirement : IAuthorizationRequirement;

public class ApprovedCustomerHandler(CustomerAccessService customerAccessService) : AuthorizationHandler<ApprovedCustomerRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ApprovedCustomerRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;

        if (context.User.IsInRole("Administrador") || await customerAccessService.HasApprovedCustomerAccessAsync(userId))
        {
            context.Succeed(requirement);
        }
    }
}

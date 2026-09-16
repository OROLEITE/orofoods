using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Orofoods.Web.Models.Identity;
using Orofoods.Web.Services.Identity;

namespace Orofoods.Web.Controllers.Api.V1;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    CustomerAccessService customerAccessService,
    ApiTokenService tokenService) : ControllerBase
{
    [HttpPost("token")]
    public async Task<ActionResult<ApiTokenResult>> Token(TokenRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || !user.IsActive) return Unauthorized();

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded) return Unauthorized();

        if (!await customerAccessService.HasApprovedCustomerAccessAsync(user.Id)) return Forbid();

        return Ok(await tokenService.CreateAsync(user));
    }
}

public sealed record TokenRequest(string Email, string Password);

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Services.Identity;

public sealed class ApiTokenService(IConfiguration configuration, UserManager<ApplicationUser> userManager)
{
    public async Task<ApiTokenResult?> CreateAsync(ApplicationUser user)
    {
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT não configurado.");
        var roles = await userManager.GetRolesAsync(user);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id), new(JwtRegisteredClaimNames.Email, user.Email ?? "") };
        if (user.CustomerId is int customerId) claims.Add(new Claim("customer_id", customerId.ToString()));
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var expires = DateTime.UtcNow.AddHours(8);
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"], audience: configuration["Jwt:Audience"], claims: claims,
            expires: expires, signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new ApiTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public sealed record ApiTokenResult(string AccessToken, DateTime ExpiresAt);

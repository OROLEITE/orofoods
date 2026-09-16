using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orofoods.Web.Data;
using Orofoods.Web.Models.Identity;

namespace Orofoods.Web.Tests.Infrastructure;

internal static class TestIdentityFactory
{
    public static UserManager<ApplicationUser> CreateUserManager(ApplicationDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            new Logger<UserManager<ApplicationUser>>(new LoggerFactory()));
    }

    public static RoleManager<IdentityRole> CreateRoleManager(ApplicationDbContext db)
    {
        var store = new RoleStore<IdentityRole>(db);
        return new RoleManager<IdentityRole>(
            store,
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new Logger<RoleManager<IdentityRole>>(new LoggerFactory()));
    }
}

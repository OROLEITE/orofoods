using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Orofoods.Web.Infrastructure;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class AuthenticatedResponseCacheMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_prevents_browser_caching_for_an_authenticated_user()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"));
        var middleware = new AuthenticatedResponseCacheMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("no-store, no-cache, max-age=0", context.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-cache", context.Response.Headers.Pragma.ToString());
    }

    [Fact]
    public async Task InvokeAsync_keeps_public_response_cache_headers_unchanged_for_an_anonymous_user()
    {
        var context = new DefaultHttpContext();
        var middleware = new AuthenticatedResponseCacheMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.False(context.Response.Headers.ContainsKey("Cache-Control"));
    }
}

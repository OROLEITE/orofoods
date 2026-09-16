using Microsoft.AspNetCore.Http;
using Orofoods.Web.Infrastructure;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_adds_the_standard_browser_security_headers()
    {
        var context = new DefaultHttpContext();
        var middleware = new SecurityHeadersMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.Equal("DENY", context.Response.Headers["X-Frame-Options"].ToString());
        Assert.Equal("strict-origin-when-cross-origin", context.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("geolocation=(), camera=(), microphone=()", context.Response.Headers["Permissions-Policy"].ToString());
        Assert.Contains("default-src 'self'", context.Response.Headers["Content-Security-Policy"].ToString());
        Assert.Contains("object-src 'none'", context.Response.Headers["Content-Security-Policy"].ToString());
    }
}

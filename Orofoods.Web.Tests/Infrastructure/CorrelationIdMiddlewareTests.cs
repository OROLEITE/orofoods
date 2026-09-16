using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Orofoods.Web.Infrastructure;

namespace Orofoods.Web.Tests.Infrastructure;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_preserves_a_valid_request_correlation_identifier_in_the_response()
    {
        var correlationId = "e72342b5-8f4f-4daa-a096-8172e89e9968";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, NullLogger<CorrelationIdMiddleware>.Instance);

        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_replaces_an_invalid_request_correlation_identifier()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = "not-a-guid";
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context, NullLogger<CorrelationIdMiddleware>.Instance);

        Assert.True(Guid.TryParse(context.Response.Headers[CorrelationIdMiddleware.HeaderName], out _));
    }
}

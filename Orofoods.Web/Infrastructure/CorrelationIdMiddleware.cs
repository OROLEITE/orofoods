using Microsoft.Extensions.Logging;

namespace Orofoods.Web.Infrastructure;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var requestedCorrelationId = context.Request.Headers[HeaderName].ToString();
        var correlationId = Guid.TryParse(requestedCorrelationId, out var parsedCorrelationId)
            ? parsedCorrelationId.ToString("D")
            : Guid.NewGuid().ToString("D");

        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { [ItemName] = correlationId }))
        {
            await next(context);
        }
    }
}

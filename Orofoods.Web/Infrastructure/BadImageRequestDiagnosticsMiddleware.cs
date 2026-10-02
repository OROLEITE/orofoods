using Orofoods.Web.Infrastructure.Logging;

namespace Orofoods.Web.Infrastructure;

public sealed class BadImageRequestDiagnosticsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILogger<BadImageRequestDiagnosticsMiddleware> logger)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (BadImageRuntimeDiagnostics.FindBadImageException(exception) is not null)
        {
            BadImageRuntimeDiagnostics.LogRequestFailure(logger, context, exception);
            throw;
        }
    }
}

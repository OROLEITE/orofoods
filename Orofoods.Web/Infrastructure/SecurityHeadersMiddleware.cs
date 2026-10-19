namespace Orofoods.Web.Infrastructure;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data: https://www.mercadolibre.com https://www.mercadolivre.com https://www.bimboqsrbrasil.com.br https://bbrwebsiapscp19.azurewebsites.net https://loja.burgerfoods.com.br; font-src 'self' https://fonts.gstatic.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; script-src 'self' 'unsafe-inline' https://sdk.mercadopago.com; connect-src 'self' https://api.mercadopago.com https://events.mercadopago.com https://api.mercadolibre.com https://www.mercadolibre.com https://www.mercadolivre.com";

        return next(context);
    }
}

namespace ReneB.Portal.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var publicRecruiter = context.Request.Path == "/recruiters";
            var privateRoute = context.Request.Path.StartsWithSegments("/auth")
                || context.Request.Path.StartsWithSegments("/portal")
                || context.Request.Path.StartsWithSegments("/admin")
                || context.Request.Path == "/readyz";

            context.Response.Headers["Content-Security-Policy"] = publicRecruiter
                ? "default-src 'self'; base-uri 'self'; connect-src 'self' https://stats.reneb.au; font-src 'none'; form-action 'self'; frame-ancestors 'none'; img-src 'self'; object-src 'none'; script-src 'self' https://stats.reneb.au; style-src 'self'"
                : "default-src 'self'; base-uri 'self'; connect-src 'self'; font-src 'none'; form-action 'self'; frame-ancestors 'none'; img-src 'self'; object-src 'none'; script-src 'self'; style-src 'self'";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            if (privateRoute)
            {
                context.Response.Headers["Cache-Control"] = "no-store, max-age=0";
                context.Response.Headers.Append("Pragma", "no-cache");
                context.Response.Headers.Append("X-Robots-Tag", "noindex, nofollow, noarchive");
            }
            return Task.CompletedTask;
        });

        await next(context);
    }
}

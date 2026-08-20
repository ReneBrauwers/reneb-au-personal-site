using System.Security.Cryptography;

namespace ReneB.Portal.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var publicAnalytics = context.Request.Path == "/" || context.Request.Path == "/recruiters";
        var analyticsOrigin = "https://stats.reneb.au";
        var analyticsEnabled = false;
        if (publicAnalytics)
        {
            var database = context.RequestServices.GetRequiredService<ReneB.Portal.Data.PortalDatabase>();
            var settings = (await database.GetContentAsync<ReneB.Portal.Models.SiteSettingsContent>(ReneB.Portal.Models.ContentDocumentKeys.SiteSettings, false, context.RequestAborted)).Content;
            analyticsOrigin = new Uri(settings.UmamiScriptUrl).GetLeftPart(UriPartial.Authority);
            analyticsEnabled = settings.AnalyticsEnabled;
        }
        var scriptNonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        context.Items["ScriptNonce"] = scriptNonce;
        context.Response.OnStarting(() =>
        {
            var privateRoute = context.Request.Path.StartsWithSegments("/auth")
                || context.Request.Path.StartsWithSegments("/portal")
                || context.Request.Path.StartsWithSegments("/admin")
                || context.Request.Path == "/readyz";

            var analyticsPolicy = analyticsEnabled ? $" {analyticsOrigin}" : string.Empty;
            var qrEnrollmentRoute = string.Equals(context.Request.Path.Value?.TrimEnd('/'), "/admin/totp", StringComparison.OrdinalIgnoreCase);
            var imagePolicy = qrEnrollmentRoute ? "'self' data:" : "'self'";
            context.Response.Headers["Content-Security-Policy"] = publicAnalytics
                ? $"default-src 'self'; base-uri 'self'; connect-src 'self'{analyticsPolicy}; font-src 'none'; form-action 'self'; frame-ancestors 'none'; img-src 'self'; object-src 'none'; script-src 'self' 'nonce-{scriptNonce}'{analyticsPolicy}; style-src 'self'"
                : $"default-src 'self'; base-uri 'self'; connect-src 'self'; font-src 'none'; form-action 'self'; frame-ancestors 'none'; img-src {imagePolicy}; object-src 'none'; script-src 'self'; style-src 'self'";
            context.Response.Headers["Referrer-Policy"] = privateRoute ? "no-referrer" : "strict-origin-when-cross-origin";
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

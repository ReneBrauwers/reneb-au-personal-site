using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PortalOptions>().Bind(builder.Configuration.GetSection("Portal"));
builder.Services.AddOptions<EncryptionOptions>().Bind(builder.Configuration.GetSection("Encryption"));
builder.Services.AddOptions<MailOptions>().Bind(builder.Configuration.GetSection("Mail"));
builder.Services.AddOptions<CookieKeyProtectionOptions>().Bind(builder.Configuration.GetSection("CookieKeyProtection"));
builder.Services.PostConfigure<PortalOptions>(options =>
{
    var adminEmails = builder.Configuration["ADMIN_EMAILS"];
    if (!string.IsNullOrWhiteSpace(adminEmails))
    {
        options.AdminEmails = adminEmails.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
    var untrustedEmailDomains = builder.Configuration["UNTRUSTED_EMAIL_DOMAINS"];
    if (untrustedEmailDomains is not null)
    {
        options.UntrustedEmailDomains = untrustedEmailDomains.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
    var disposableEmailDomains = builder.Configuration["DISPOSABLE_EMAIL_DOMAINS"];
    if (disposableEmailDomains is not null)
    {
        options.DisposableEmailDomains = disposableEmailDomains.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
    if (bool.TryParse(builder.Configuration["RECRUITER_PORTAL_ENABLED"], out var enabled))
    {
        options.Enabled = enabled;
    }
});

var dataDirectory = builder.Configuration["Portal:DataDirectory"] ?? "/app/data";
Directory.CreateDirectory(dataDirectory);
var dataProtection = builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDirectory, "data-protection")))
    .SetApplicationName("reneb-au-recruiter-portal");
var cookieCertificatePath = builder.Configuration["CookieKeyProtection:CertificatePath"];
var cookieKeyPath = builder.Configuration["CookieKeyProtection:PrivateKeyPath"];
if (File.Exists(cookieCertificatePath) && File.Exists(cookieKeyPath))
{
    dataProtection.ProtectKeysWithCertificate(X509Certificate2.CreateFromPemFile(cookieCertificatePath, cookieKeyPath));
}
else if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("The production Data Protection certificate and private key are required.");
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FieldEncryptionService>();
builder.Services.AddSingleton<PortalDatabase>();
builder.Services.AddSingleton<IdentityService>();
builder.Services.AddScoped<SessionCookieEvents>();
builder.Services.AddSingleton<PdfValidator>();
builder.Services.AddScoped<IAuthorizationHandler, RecruiterAccessHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminBaseHandler>();
builder.Services.AddScoped<IAuthorizationHandler, AdminTotpHandler>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Portal", "Recruiter");
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = builder.Environment.IsDevelopment() ? "reneb-portal-dev" : "__Host-reneb-portal";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.Cookie.Path = "/";
        options.LoginPath = "/auth/login";
        options.AccessDeniedPath = "/auth/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.EventsType = typeof(SessionCookieEvents);
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Recruiter", policy => policy.RequireAuthenticatedUser().AddRequirements(new RecruiterAccessRequirement()))
    .AddPolicy("AdminBase", policy => policy.RequireAuthenticatedUser().AddRequirements(new AdminBaseRequirement()))
    .AddPolicy("Admin", policy => policy.RequireAuthenticatedUser().AddRequirements(new AdminTotpRequirement()));
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment() ? "reneb-csrf-dev" : "__Host-reneb-csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.SuppressXFrameOptionsHeader = true;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = context.RequestServices.GetRequiredService<IOptions<PortalOptions>>().Value.AuthRequestsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

builder.Services.AddHttpClient<GraphMailTransport>();
builder.Services.AddScoped<IMailTransport>(services =>
{
    var options = services.GetRequiredService<IOptions<MailOptions>>().Value;
    return string.Equals(options.Mode, "Development", StringComparison.OrdinalIgnoreCase)
        ? new DevelopmentMailTransport(services.GetRequiredService<PortalDatabase>())
        : services.GetRequiredService<GraphMailTransport>();
});
builder.Services.AddHostedService<OutboxWorker>();
builder.Services.AddHostedService<RetentionWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.GetRequiredService<PortalDatabase>().MigrateAsync();
}

if (args.Length > 0 && args[0] is "migrate" or "backup" or "restore-check")
{
    var database = app.Services.GetRequiredService<PortalDatabase>();
    if (args[0] == "migrate")
    {
        await database.MigrateAsync();
        await database.ReconcileAdministratorsAsync(app.Services.GetRequiredService<IOptions<PortalOptions>>().Value.AdminEmails);
        Console.WriteLine("Portal schema migration completed.");
        return;
    }
    if (args[0] == "backup")
    {
        var backup = await database.BackupAsync();
        Console.WriteLine(backup);
        return;
    }

    var path = args.Length > 1
        ? args[1]
        : Directory.GetFiles(app.Configuration["Portal:BackupDirectory"] ?? "/app/backups", "*.enc")
            .OrderByDescending(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
    if (path is null || !await database.RestoreCheckAsync(path))
    {
        Console.Error.WriteLine("Encrypted backup restore verification failed.");
        Environment.ExitCode = 1;
        return;
    }
    Console.WriteLine($"Encrypted backup restore verification passed: {path}");
    return;
}

await app.Services.GetRequiredService<PortalDatabase>().ReconcileAdministratorsAsync(
    app.Services.GetRequiredService<IOptions<PortalOptions>>().Value.AdminEmails);

var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 2
};
var trustedProxyNetworks = app.Configuration["Portal:TrustedProxyNetworks"]
    ?.Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
foreach (var value in trustedProxyNetworks)
{
    if (!System.Net.IPNetwork.TryParse(value, out var network))
    {
        throw new InvalidOperationException($"Trusted proxy network '{value}' is not valid CIDR notation.");
    }
    forwardedHeaders.KnownIPNetworks.Add(network);
}
if (!app.Environment.IsDevelopment() && trustedProxyNetworks.Length == 0)
{
    throw new InvalidOperationException("At least one trusted reverse-proxy network is required in production.");
}
app.UseForwardedHeaders(forwardedHeaders);
app.UseExceptionHandler("/error");
app.UseMiddleware<SecurityHeadersMiddleware>();
app.Use(async (context, next) =>
{
    var options = context.RequestServices.GetRequiredService<IOptionsMonitor<PortalOptions>>().CurrentValue;
    var normalizedPath = context.Request.Path.Value?.TrimEnd('/') is { Length: > 0 } value ? value : "/";
    var gatedPublicPath = string.Equals(normalizedPath, "/recruiters", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/llms.txt", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/candidate.json", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/recruiters/profile.md", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/portal", StringComparison.OrdinalIgnoreCase)
        || normalizedPath.StartsWith("/portal/", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/auth/register", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/auth", StringComparison.OrdinalIgnoreCase)
        || string.Equals(normalizedPath, "/auth/login", StringComparison.OrdinalIgnoreCase);
    if (!options.Enabled && gatedPublicPath)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next(context);
});
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = "/portal-assets",
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "public, max-age=86400"
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Text("healthy\n", "text/plain"));
app.MapGet("/readyz", async (PortalDatabase database, IOptions<MailOptions> mail, CancellationToken cancellationToken) =>
{
    var databaseReady = await database.IsReadyAsync(cancellationToken);
    var mailReady = MailConfigurationValidator.IsReady(mail.Value);
    return databaseReady && mailReady
        ? Results.Text("ready\n", "text/plain")
        : Results.Text("not ready\n", "text/plain", statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/llms.txt", async (PortalDatabase database, CancellationToken cancellationToken) =>
    Results.Text(PublicProfileRenderer.ToLlmsText(await database.GetPublicProfileAsync(false, cancellationToken)), "text/plain; charset=utf-8"));
app.MapGet("/recruiters/profile.md", async (PortalDatabase database, CancellationToken cancellationToken) =>
    Results.Text(PublicProfileRenderer.ToMarkdown(await database.GetPublicProfileAsync(false, cancellationToken)), "text/markdown; charset=utf-8"));
app.MapGet("/candidate.json", async (PortalDatabase database, CancellationToken cancellationToken) =>
    Results.Text(PublicProfileRenderer.ToJson(await database.GetPublicProfileAsync(false, cancellationToken)), "application/json; charset=utf-8"));

if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev/mail", async (PortalDatabase database, CancellationToken cancellationToken) =>
        Results.Json(await database.ListDevelopmentMailAsync(cancellationToken)));
}

app.MapRazorPages();
app.Run();

public partial class Program;

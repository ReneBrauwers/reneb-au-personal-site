using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ReneB.Portal.Tests;

public sealed class PortalFactory : WebApplicationFactory<Program>
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"reneb-portal-tests-{Guid.NewGuid():N}");
    public AdjustableTimeProvider Time { get; } = new(new DateTimeOffset(2026, 8, 12, 0, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_directory);
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Portal:Enabled"] = "true",
            ["Portal:DataDirectory"] = Path.Combine(_directory, "data"),
            ["Portal:BackupDirectory"] = Path.Combine(_directory, "backups"),
            ["Portal:CanonicalBaseUrl"] = "https://reneb.au",
            ["Portal:AuthRequestsPerMinute"] = "1000",
            ["Portal:AdminEmails:0"] = "admin@example.invalid",
            ["Portal:UntrustedEmailDomains:0"] = "gmail.com",
            ["Portal:DisposableEmailDomains:0"] = "mailinator.com",
            ["Encryption:AllowDevelopmentKey"] = "true",
            ["Encryption:KeyFile"] = Path.Combine(_directory, "missing-keyring.json"),
            ["Mail:Mode"] = "Development",
            ["AllowedHosts"] = "localhost;127.0.0.1"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_directory))
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Microsoft.Data.Sqlite can release the pooled test handle just after host disposal.
            }
        }
    }
}

public sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan duration) => _now = _now.Add(duration);
}

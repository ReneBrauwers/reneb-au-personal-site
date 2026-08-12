using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ReneB.Portal.Tests;

public sealed class PublicDiscoveryTests : IClassFixture<PortalFactory>
{
    private readonly HttpClient _client;

    public PublicDiscoveryTests(PortalFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task PublicRepresentationsShareCanonicalEvidenceWithoutPrivateTerms()
    {
        var routes = new[] { "/recruiters", "/llms.txt", "/recruiters/profile.md", "/candidate.json" };
        foreach (var route in routes)
        {
            var response = await _client.GetAsync(route);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.Contains("Ren", content, StringComparison.Ordinal);
            Assert.Contains("enterprise architecture", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("https://reneb.au", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("A$", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("per day", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("transition payment", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task CandidateJsonHasVersionedPublicContract()
    {
        var response = await _client.GetAsync("/candidate.json");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("1.0", json.RootElement.GetProperty("schemaVersion").GetString());
        Assert.True(json.RootElement.GetProperty("candidateSupplied").GetBoolean());
        Assert.Equal("https://reneb.au/recruiters", json.RootElement.GetProperty("candidate").GetProperty("canonicalProfile").GetString());
        Assert.Equal("Available after verified access", json.RootElement.GetProperty("disclosure").GetProperty("compensation").GetString());
    }

    [Fact]
    public async Task PrivateAndAuthRoutesAreNotIndexedOrTracked()
    {
        var register = await _client.GetAsync("/auth/register");
        var registerContent = await register.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        Assert.DoesNotContain("stats.reneb.au", registerContent, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("noindex, nofollow, noarchive", register.Headers.GetValues("X-Robots-Tag"));
        Assert.Contains("no-store", register.Headers.CacheControl?.ToString());

        var portal = await _client.GetAsync("/portal");
        Assert.Equal(HttpStatusCode.Redirect, portal.StatusCode);
        Assert.Contains("/auth/login", portal.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.Contains("noindex, nofollow, noarchive", portal.Headers.GetValues("X-Robots-Tag"));
    }

    [Fact]
    public async Task RecruiterPageAloneLoadsApprovedAnalytics()
    {
        var recruiter = await _client.GetStringAsync("/recruiters");
        Assert.Contains("https://stats.reneb.au/script.js", recruiter, StringComparison.Ordinal);
        Assert.Contains("data-exclude-search=\"true\"", recruiter, StringComparison.Ordinal);
        Assert.Contains("data-do-not-track=\"true\"", recruiter, StringComparison.Ordinal);
        Assert.Contains("application/ld+json", recruiter, StringComparison.Ordinal);
        var start = recruiter.IndexOf("<script type=\"application/ld+json\">", StringComparison.Ordinal)
            + "<script type=\"application/ld+json\">".Length;
        var end = recruiter.IndexOf("</script>", start, StringComparison.Ordinal);
        using var jsonLd = JsonDocument.Parse(recruiter[start..end]);
        Assert.Equal("https://schema.org", jsonLd.RootElement.GetProperty("@context").GetString());
        Assert.Equal("ProfilePage", jsonLd.RootElement.GetProperty("@type").GetString());
        Assert.Equal("Demand", jsonLd.RootElement.GetProperty("mainEntity").GetProperty("seeks").GetProperty("@type").GetString());
    }

    [Fact]
    public async Task PrivacyNoticeIsPublicAndHasNoTracking()
    {
        var response = await _client.GetAsync("/privacy");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Australian Privacy Principles", body);
        Assert.DoesNotContain("stats.reneb.au", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DisabledLaunchModeHidesDiscoveryButKeepsAdministratorBootstrap()
    {
        await using var disabledFactory = new PortalFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Portal:Enabled"] = "false" })));
        using var client = disabledFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/recruiters")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/auth")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/auth/login")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/auth/login/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/auth/register/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/AUTH/REGISTER/")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/recruiters/")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/complete")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/privacy")).StatusCode);
    }

    [Fact]
    public async Task DisabledLaunchModeStillRequiresBootstrapMailConfigurationForReadiness()
    {
        await using var disabledFactory = new PortalFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Portal:Enabled"] = "false",
                    ["Mail:Mode"] = "Graph",
                    ["Mail:TenantId"] = string.Empty,
                    ["Mail:ClientId"] = string.Empty,
                    ["Mail:SenderMailbox"] = string.Empty
                })));
        using var client = disabledFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/readyz")).StatusCode);
    }
}

using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class HomePageTests : IClassFixture<PortalFactory>
{
    private readonly HttpClient _client;

    public HomePageTests(PortalFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task HomepageShowsSystemsThinkingProjectsAndGitHub()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Contains("Protect the outcome.<span>Make the trade-offs visible.</span>", html, StringComparison.Ordinal);
        Assert.Contains("id=\"systems\"", html, StringComparison.Ordinal);
        Assert.Contains("Root cause before the fix", html, StringComparison.Ordinal);
        Assert.Contains("Prevention over recovery", html, StringComparison.Ordinal);
        Assert.Contains("id=\"projects\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#projects\"", html, StringComparison.Ordinal);
        Assert.Contains("Unreleased prototype", html, StringComparison.Ordinal);
        Assert.Contains("An automated asset-accumulation system", html, StringComparison.Ordinal);
        Assert.Contains("Enterprise integration tooling", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://github.com/ReneBrauwers\"", html, StringComparison.Ordinal);

        // Projects are described, never linked.
        var start = html.IndexOf("id=\"projects\"", StringComparison.Ordinal);
        var end = html.IndexOf("</section>", start, StringComparison.Ordinal);
        var projects = html[start..end];
        Assert.DoesNotContain("<a ", projects, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=", projects, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mazerealm.com", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com/ReneBrauwers/", html, StringComparison.OrdinalIgnoreCase);

        // No email address is published; recruiters sign in to the portal instead.
        Assert.DoesNotContain("mailto:", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("id=\"recruiter-contact\"", html, StringComparison.Ordinal);
        Assert.Contains("Sign in to the recruiter portal", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/auth/login\" data-umami-event=\"recruiter-signin-contact\">Recruiter sign in</a>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/auth/login\" data-umami-event=\"recruiter-signin-header\">Recruiter sign in</a>", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/auth/register\" data-umami-event=\"recruiter-access-start\">Request recruiter access</a>", html, StringComparison.Ordinal);

        var login = await _client.GetAsync("/auth/login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        // The advisory-business placeholder is a server-side comment and must not ship in HTML.
        Assert.DoesNotContain("advisory-business", html, StringComparison.OrdinalIgnoreCase);
    }

    public static readonly string[] KnownProjectNames =
    [
        "Maze Realm", "MazeRealm", "maze-realm", "Void Labyrinth", "RPG-LLM-Empire", "Equine Vet OS", "equine-vet-os",
        "XRPL Accumulator", "xrpl-accumulator", "XRPL", "XRP", "CryptoTaxMan", "AssetTransactionTracker", "crypto-transaction-report",
        "Soulforge", "Roomagotchi", "MoodCanvas", "BizTalk", "Global Integration Bootcamp", "reneb.au", "Coogee", "CoinGecko", "Sologenic",
        "ArchGovHub", "Kuntsult", "hgdmogoh"
    ];

    [Fact]
    public async Task ProjectsSectionNamesNoProjects()
    {
        var html = WebUtility.HtmlDecode(await _client.GetStringAsync("/"));
        var start = html.IndexOf("id=\"projects\"", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var projects = html[start..html.IndexOf("</section>", start, StringComparison.Ordinal)];
        foreach (var name in KnownProjectNames)
        {
            Assert.DoesNotContain(name, projects, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var card in HomeSectionDefaults.Projects())
        {
            foreach (var name in KnownProjectNames)
            {
                Assert.DoesNotContain(name, card.Heading + " " + card.Status + " " + card.Detail, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void PreviouslyPublishedHomeDocumentGainsNewSectionDefaults()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(ContentDefaults.Home(), ContentTypeRegistry.JsonOptions))!.AsObject();
        foreach (var key in json.Select(p => p.Key).Where(k => k.StartsWith("systems", StringComparison.Ordinal) || k.StartsWith("projects", StringComparison.Ordinal) || k.StartsWith("recruiterContact", StringComparison.Ordinal) || k.StartsWith("recruiterSignIn", StringComparison.Ordinal) || k.StartsWith("recruiterRequest", StringComparison.Ordinal)).ToList())
        {
            json.Remove(key);
        }

        var home = Assert.IsType<HomePageContent>(ContentTypeRegistry.DeserializeAndValidate(ContentDocumentKeys.Home, json.ToJsonString()));
        Assert.Equal(8, home.Projects.Count);
        Assert.Equal("Recruiter sign in", home.RecruiterSignInAction);
        Assert.Equal(5, home.SystemsPractices.Count);
        Assert.Equal("Make the trade-offs visible.", home.HeroHeadingLineTwo);

        var settings = Assert.IsType<SiteSettingsContent>(ContentTypeRegistry.DeserializeAndValidate(
            ContentDocumentKeys.SiteSettings, "{\"seoTitle\":\"t\",\"metaDescription\":\"d\",\"socialTitle\":\"s\",\"socialDescription\":\"d\",\"socialImageUrl\":\"https://reneb.au/social-card.png\",\"linkedInUrl\":\"https://www.linkedin.com/in/renebrauwers/\",\"xUrl\":\"https://x.com/Rene_B\",\"xHandle\":\"@Rene_B\",\"footerNotice\":\"f\"}"));
        Assert.Equal("https://github.com/ReneBrauwers", settings.GitHubUrl);
    }

    [Fact]
    public void ProjectCardsHaveNoLinkFieldsAndGitHubUrlMustBeHttps()
    {
        Assert.Null(typeof(ProjectCard).GetProperty("LinkUrl"));
        Assert.Null(typeof(SiteSettingsContent).GetProperty("ContactEmail"));

        var settings = ContentDefaults.SiteSettings();
        settings.GitHubUrl = "http://github.com/ReneBrauwers";
        Assert.Throws<ValidationException>(() => ContentTypeRegistry.ValidateGraph(settings));
    }
}

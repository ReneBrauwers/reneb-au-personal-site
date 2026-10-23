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
        Assert.Contains("https://mazerealm.com/", html, StringComparison.Ordinal);
        Assert.Contains("Unreleased prototype", html, StringComparison.Ordinal);
        Assert.Contains("href=\"https://github.com/ReneBrauwers\"", html, StringComparison.Ordinal);

        // Private repositories must never be linked from the public site.
        Assert.DoesNotContain("github.com/ReneBrauwers/equine-vet-os", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com/ReneBrauwers/void-labyrinth", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com/ReneBrauwers/maze-realm", html, StringComparison.OrdinalIgnoreCase);
        // No public mailbox is configured by default, so no mailto link is rendered.
        Assert.DoesNotContain("mailto:", html, StringComparison.OrdinalIgnoreCase);
        // The advisory-business placeholder is a server-side comment and must not ship in HTML.
        Assert.DoesNotContain("advisory-business", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviouslyPublishedHomeDocumentGainsNewSectionDefaults()
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(ContentDefaults.Home(), ContentTypeRegistry.JsonOptions))!.AsObject();
        foreach (var key in json.Select(p => p.Key).Where(k => k.StartsWith("systems", StringComparison.Ordinal) || k.StartsWith("projects", StringComparison.Ordinal)).ToList())
        {
            json.Remove(key);
        }

        var home = Assert.IsType<HomePageContent>(ContentTypeRegistry.DeserializeAndValidate(ContentDocumentKeys.Home, json.ToJsonString()));
        Assert.Equal(4, home.Projects.Count);
        Assert.Equal(5, home.SystemsPractices.Count);
        Assert.Equal("Make the trade-offs visible.", home.HeroHeadingLineTwo);

        var settings = Assert.IsType<SiteSettingsContent>(ContentTypeRegistry.DeserializeAndValidate(
            ContentDocumentKeys.SiteSettings, "{\"seoTitle\":\"t\",\"metaDescription\":\"d\",\"socialTitle\":\"s\",\"socialDescription\":\"d\",\"socialImageUrl\":\"https://reneb.au/social-card.png\",\"linkedInUrl\":\"https://www.linkedin.com/in/renebrauwers/\",\"xUrl\":\"https://x.com/Rene_B\",\"xHandle\":\"@Rene_B\",\"footerNotice\":\"f\"}"));
        Assert.Equal("https://github.com/ReneBrauwers", settings.GitHubUrl);
        Assert.Equal(string.Empty, settings.ContactEmail);
    }

    [Fact]
    public void ProjectLinksMustBeHttps()
    {
        var home = ContentDefaults.Home();
        home.Projects[0].LinkUrl = "http://mazerealm.com/";
        Assert.Throws<ValidationException>(() => ContentTypeRegistry.ValidateGraph(home));

        var settings = ContentDefaults.SiteSettings();
        settings.ContactEmail = "not-an-email";
        Assert.Throws<ValidationException>(() => ContentTypeRegistry.ValidateGraph(settings));
    }
}

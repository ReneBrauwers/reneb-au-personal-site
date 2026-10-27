using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class PublicMetadataAndEventsTests : IClassFixture<PortalFactory>
{
    // The employer is not published. The names live only in this test so a regression is caught.
    private static readonly string[] EmployerNames = ["EMPLOYER", "EMPLOYER Limited", "EMPLOYER Group", "EMPLOYER"];

    private static readonly string[] PublicRoutes =
    [
        "/", "/recruiters", "/llms.txt", "/recruiters/profile.md", "/candidate.json", "/robots.txt", "/sitemap.xml", "/privacy"
    ];

    private readonly PortalFactory _factory;
    private readonly HttpClient _client;

    public PublicMetadataAndEventsTests(PortalFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    private static string Meta(string html, string attribute, string key)
    {
        var match = Regex.Match(html, $"<meta {attribute}=\"{Regex.Escape(key)}\" content=\"([^\"]*)\">");
        Assert.True(match.Success, $"Missing <meta {attribute}=\"{key}\">");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static string Head(string html) => html[..html.IndexOf("</head>", StringComparison.Ordinal)];

    [Fact]
    public async Task RecruiterPageHasDescriptionOpenGraphTwitterCanonicalAndProfilePageJsonLd()
    {
        var html = await _client.GetStringAsync("/recruiters");
        var head = Head(html);

        var description = Meta(head, "name", "description");
        Assert.InRange(description.Length, 50, 200);
        Assert.Contains("René Brauwers", description, StringComparison.Ordinal);

        Assert.Equal("profile", Meta(head, "property", "og:type"));
        Assert.Equal("https://reneb.au/recruiters", Meta(head, "property", "og:url"));
        Assert.Equal("For recruiters and talent agents - René Brauwers", Meta(head, "property", "og:title"));
        Assert.Equal(description, Meta(head, "property", "og:description"));
        Assert.Equal("https://reneb.au/social-card.png", Meta(head, "property", "og:image"));
        Assert.Equal("summary_large_image", Meta(head, "name", "twitter:card"));
        Assert.Equal("For recruiters and talent agents - René Brauwers", Meta(head, "name", "twitter:title"));
        Assert.Equal(description, Meta(head, "name", "twitter:description"));
        Assert.Equal("https://reneb.au/social-card.png", Meta(head, "name", "twitter:image"));
        Assert.Equal("@Rene_B", Meta(head, "name", "twitter:creator"));

        Assert.Single(Regex.Matches(head, "<link rel=\"canonical\" href=\"https://reneb.au/recruiters\">"));
        Assert.Contains("<meta name=\"robots\" content=\"index,follow,max-image-preview:large\">", head, StringComparison.Ordinal);

        var match = Regex.Match(head, "<script type=\"application/ld\\+json\" nonce=\"[^\"]+\">(.*?)</script>", RegexOptions.Singleline);
        Assert.True(match.Success, "ProfilePage JSON-LD must be in the document head");
        using var json = JsonDocument.Parse(match.Groups[1].Value);
        var root = json.RootElement;
        Assert.Equal("https://schema.org", root.GetProperty("@context").GetString());
        Assert.Equal("ProfilePage", root.GetProperty("@type").GetString());
        Assert.Equal("https://reneb.au/recruiters", root.GetProperty("url").GetString());
        Assert.Equal("en-AU", root.GetProperty("inLanguage").GetString());
        Assert.Equal(description, root.GetProperty("description").GetString());
        var person = root.GetProperty("mainEntity");
        Assert.Equal("Person", person.GetProperty("@type").GetString());
        Assert.Equal("René Brauwers", person.GetProperty("name").GetString());
        Assert.False(person.TryGetProperty("worksFor", out _), "worksFor must not be published");
        var sameAs = person.GetProperty("sameAs").EnumerateArray().Select(item => item.GetString()).ToArray();
        Assert.Contains("https://www.linkedin.com/in/renebrauwers/", sameAs);
        Assert.Contains("https://github.com/ReneBrauwers", sameAs);
    }

    [Fact]
    public async Task PublicRoutesNeverContainTheEmployerName()
    {
        foreach (var route in PublicRoutes)
        {
            var body = WebUtility.HtmlDecode(await _client.GetStringAsync(route));
            foreach (var name in EmployerNames)
            {
                Assert.False(body.Contains(name, StringComparison.OrdinalIgnoreCase), $"{route} mentions the employer");
            }
            Assert.DoesNotContain("worksFor", body, StringComparison.Ordinal);
            Assert.DoesNotContain("currentEmployer", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PublicProfileDocumentsAndDefaultsDoNotCarryAnEmployerField()
    {
        Assert.Null(typeof(PublicCandidateProfile).GetProperty("CurrentEmployer"));
        var candidate = await _client.GetStringAsync("/candidate.json");
        Assert.DoesNotContain("employer", candidate, StringComparison.OrdinalIgnoreCase);

        var defaults = JsonSerializer.Serialize(ContentDefaults.Home(), ContentTypeRegistry.JsonOptions)
            + JsonSerializer.Serialize(PublicProfileDefaults.Create(), ContentTypeRegistry.JsonOptions)
            + JsonSerializer.Serialize(ContentDefaults.SiteSettings(), ContentTypeRegistry.JsonOptions);
        Assert.False(EmployerRedaction.Mentions(defaults));
    }

    [Fact]
    public void EmployerRedactionRewritesStoredContentWithoutLeavingBrokenSentences()
    {
        var home = ContentDefaults.Home();
        home.ProofItems[0].Detail = "EMPLOYER";
        home.FoundationBody = RichTextContent.FromParagraphs("Today I lead Enterprise Architecture at EMPLOYER, working across Business and Risk.");
        home.ClosingEyebrow = "EMPLOYER and others";
        EmployerRedaction.Apply(home);

        Assert.Equal("Enterprise architecture leadership", home.ProofItems[0].Detail);
        Assert.Contains("Enterprise Architecture in regulated financial services, working across Business and Risk.", home.FoundationBody.DeltaJson, StringComparison.Ordinal);
        Assert.Equal("a regulated financial-services organisation and others", home.ClosingEyebrow);
        Assert.False(EmployerRedaction.Mentions(JsonSerializer.Serialize(home, ContentTypeRegistry.JsonOptions)));
        Assert.Equal("EMPLOYER motion is not an employer.", EmployerRedaction.Redact("EMPLOYER motion is not an employer."));
    }

    [Fact]
    public async Task PreviouslyPublishedContentThatNamesTheEmployerIsRedactedAtRenderTime()
    {
        var database = _factory.Services.GetRequiredService<PortalDatabase>();

        var home = await database.GetContentAsync<HomePageContent>(ContentDocumentKeys.Home, true);
        var legacyHome = ContentDefaults.Home();
        legacyHome.ProofItems[0].Detail = "EMPLOYER";
        legacyHome.FoundationBody = RichTextContent.FromParagraphs("I lead Enterprise Architecture at EMPLOYER in Sydney.");
        await database.PublishContentAsync(ContentDocumentKeys.Home, legacyHome, home.Revision, Guid.NewGuid());

        var profile = await database.GetContentAsync<PublicCandidateProfile>(ContentDocumentKeys.RecruiterProfile, true);
        var legacyProfile = profile.Content;
        legacyProfile.DemonstratedSignals = [.. legacyProfile.DemonstratedSignals, "Head of Enterprise Architecture at EMPLOYER"];
        await database.PublishContentAsync(ContentDocumentKeys.RecruiterProfile, legacyProfile, profile.Revision, Guid.NewGuid());

        foreach (var route in PublicRoutes)
        {
            var body = WebUtility.HtmlDecode(await _client.GetStringAsync(route));
            Assert.False(body.Contains("EMPLOYER", StringComparison.OrdinalIgnoreCase), $"{route} mentions the employer after legacy content was published");
        }
        Assert.Contains("in regulated financial services in Sydney", WebUtility.HtmlDecode(await _client.GetStringAsync("/")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HomepageEmitsLinkedInGitHubAndClosingSectionEventsWithoutAnEmailEvent()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(4, Regex.Matches(html, "data-umami-event=\"linkedin-click\"").Count);
        Assert.Equal(2, Regex.Matches(html, "data-umami-event=\"github-click\"").Count);
        foreach (var location in new[] { "header", "hero", "closing", "footer" })
        {
            Assert.Contains($"data-umami-event=\"linkedin-click\" data-umami-event-location=\"{location}\"", html, StringComparison.Ordinal);
        }
        foreach (var location in new[] { "closing", "footer" })
        {
            Assert.Contains($"data-umami-event=\"github-click\" data-umami-event-location=\"{location}\"", html, StringComparison.Ordinal);
        }

        // Every tracked link is a LinkedIn or GitHub link; no mailto exists, so there is no email event.
        Assert.DoesNotContain("mailto:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email-click", html, StringComparison.OrdinalIgnoreCase);

        // Closing-section scroll event: marked section plus a nonce'd inline script allowed by the existing public CSP.
        Assert.Contains("<section class=\"closing section\" data-umami-section=\"closing\"", html, StringComparison.Ordinal);
        var nonce = Regex.Match(html, "<script nonce=\"([^\"]+)\">\\s*\\(function").Groups[1].Value;
        Assert.NotEmpty(nonce);
        Assert.Contains("window.umami.track('closing-section-view')", html, StringComparison.Ordinal);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains($"script-src 'self' 'nonce-{nonce}' https://stats.reneb.au", csp, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/auth/register")]
    [InlineData("/privacy")]
    public async Task PrivatePortalAndPrivacyPagesStayUntrackedWithTheStrictCsp(string route)
    {
        var response = await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("data-website-id", html, StringComparison.Ordinal);
        Assert.DoesNotContain("umami.track", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("script.js", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stats.reneb.au", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-umami", html, StringComparison.OrdinalIgnoreCase);
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.DoesNotContain("stats.reneb.au", csp, StringComparison.Ordinal);
        Assert.DoesNotContain("nonce-", csp, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'; style-src 'self'", csp, StringComparison.Ordinal);
    }
}

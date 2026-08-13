using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class ContentStudioTests : IClassFixture<PortalFactory>
{
    private readonly PortalFactory _factory;
    private readonly PortalDatabase _database;

    public ContentStudioTests(PortalFactory factory)
    {
        _factory = factory;
        _database = factory.Services.GetRequiredService<PortalDatabase>();
    }

    [Fact]
    public async Task InitializesEveryGovernedDocumentAndKeepsPrivateProfileEncryptedFromPublicRoutes()
    {
        var documents = await _database.ListContentDocumentsAsync();
        Assert.Equal(ContentDocumentKeys.All.Order(StringComparer.Ordinal), documents.Select(item => item.Key));

        var privateDocument = await _database.GetContentAsync<PrivateCandidateProfile>(ContentDocumentKeys.OpportunityProfile, true);
        privateDocument.Content.PermanentCompensation = "PRIVATE-COMPENSATION-MARKER";
        await _database.PublishContentAsync(ContentDocumentKeys.OpportunityProfile, privateDocument.Content, privateDocument.Revision, Guid.NewGuid());

        using var client = _factory.CreateClient();
        foreach (var route in new[] { "/", "/recruiters", "/llms.txt", "/recruiters/profile.md", "/candidate.json", "/robots.txt", "/sitemap.xml" })
        {
            var body = await client.GetStringAsync(route);
            Assert.DoesNotContain("PRIVATE-COMPENSATION-MARKER", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DraftsUseOptimisticConcurrencyAndPublishAtomically()
    {
        var original = await _database.GetContentAsync<HomePageContent>(ContentDocumentKeys.Home, true);
        var first = ContentDefaults.Home();
        first.HeroHeadingLineOne = "Draft concurrency proof.";
        var saved = await _database.SaveContentDraftAsync(ContentDocumentKeys.Home, first, original.Revision, Guid.NewGuid());

        await Assert.ThrowsAsync<ContentConcurrencyException>(() =>
            _database.SaveContentDraftAsync(ContentDocumentKeys.Home, ContentDefaults.Home(), original.Revision, Guid.NewGuid()));

        await _database.PublishContentAsync(ContentDocumentKeys.Home, saved.Content, saved.Revision, Guid.NewGuid());
        using var client = _factory.CreateClient();
        Assert.Contains("Draft concurrency proof.", await client.GetStringAsync("/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UmamiConfigurationIsEditableButCannotBecomeArbitraryScriptInjection()
    {
        var original = await _database.GetContentAsync<SiteSettingsContent>(ContentDocumentKeys.SiteSettings, true);
        var settings = original.Content;
        settings.UmamiScriptUrl = "https://analytics.example.test/tracker.js";
        settings.UmamiWebsiteId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        settings.UmamiDomains = "reneb.au,www.reneb.au";
        var published = await _database.PublishContentAsync(ContentDocumentKeys.SiteSettings, settings, original.Revision, Guid.NewGuid());

        using var client = _factory.CreateClient();
        var recruiter = await client.GetStringAsync("/recruiters");
        Assert.Contains("https://analytics.example.test/tracker.js", recruiter, StringComparison.Ordinal);
        Assert.Contains("data-website-id=\"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee\"", recruiter, StringComparison.Ordinal);
        var response = await client.GetAsync("/recruiters");
        Assert.Contains("https://analytics.example.test", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);

        settings.UmamiScriptUrl = "https://analytics.example.test/tracker.js?token=secret";
        await Assert.ThrowsAsync<ValidationException>(() =>
            _database.PublishContentAsync(ContentDocumentKeys.SiteSettings, settings, published.Revision, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("{\"ops\":[{\"insert\":{\"image\":\"x\"}}]}")]
    [InlineData("{\"ops\":[{\"insert\":\"x\\n\",\"attributes\":{\"script\":true}}]}")]
    [InlineData("{\"ops\":[{\"insert\":\"x\",\"attributes\":{\"link\":\"javascript:alert(1)\"}}]}")]
    public void RichTextRejectsEmbedsUnknownAttributesAndUnsafeLinks(string delta)
        => Assert.False(RichTextDelta.TryValidate(delta, out _));

    [Fact]
    public void RichTextEncodesInsertedMarkup()
    {
        var content = new RichTextContent { DeltaJson = "{\"ops\":[{\"insert\":\"<img src=x onerror=alert(1)>\\n\"}]}" };
        var html = RichTextDelta.ToHtml(content);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;img", html, StringComparison.Ordinal);
    }
}

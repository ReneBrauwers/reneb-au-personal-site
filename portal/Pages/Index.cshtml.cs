using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages;

public sealed class IndexModel(PortalDatabase database) : PageModel
{
    public HomePageContent PageContent { get; private set; } = ContentDefaults.Home();
    public SiteSettingsContent Settings { get; private set; } = ContentDefaults.SiteSettings();
    public string JsonLd { get; private set; } = string.Empty;
    public string ScriptNonce => HttpContext.Items["ScriptNonce"] as string ?? string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        PageContent = (await database.GetContentAsync<HomePageContent>(ContentDocumentKeys.Home, false, cancellationToken)).Content;
        Settings = (await database.GetContentAsync<SiteSettingsContent>(ContentDocumentKeys.SiteSettings, false, cancellationToken)).Content;
        var profile = await database.GetPublicProfileAsync(false, cancellationToken);
        JsonLd = PublicProfileRenderer.ToJsonLd(profile).Replace("</", "<\\/", StringComparison.Ordinal);
    }

    public static string Html(RichTextContent content) => RichTextDelta.ToHtml(content);
}

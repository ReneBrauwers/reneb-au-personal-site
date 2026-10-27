using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages;

public sealed class RecruitersModel(PortalDatabase database) : PageModel
{
    public const string PageTitle = "For recruiters and talent agents";

    public PublicCandidateProfile Profile { get; private set; } = new();
    public SiteSettingsContent Settings { get; private set; } = ContentDefaults.SiteSettings();
    public string JsonLd { get; private set; } = string.Empty;
    public string MetaDescription { get; private set; } = string.Empty;
    public string SocialTitle => $"{PageTitle} - {Settings.DisplayName}";
    public DiscoveryGuidanceContent Guidance { get; private set; } = ContentDefaults.Discovery();
    public string ScriptNonce => HttpContext.Items["ScriptNonce"] as string ?? string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Profile = await database.GetPublicProfileAsync(false, cancellationToken);
        Settings = (await database.GetContentAsync<SiteSettingsContent>(ContentDocumentKeys.SiteSettings, false, cancellationToken)).Content;
        Guidance = (await database.GetContentAsync<DiscoveryGuidanceContent>(ContentDocumentKeys.Discovery, false, cancellationToken)).Content;
        MetaDescription = $"Recruiter profile for {Profile.CandidateName}: {Profile.CurrentRole}. {Profile.Headline.TrimEnd('.', ' ')}. Request verified access for private criteria.";
        JsonLd = PublicProfileRenderer.ToJsonLd(Profile, MetaDescription, [Settings.LinkedInUrl, Settings.GitHubUrl, Settings.XUrl])
            .Replace("</", "<\\/", StringComparison.Ordinal);
    }
}

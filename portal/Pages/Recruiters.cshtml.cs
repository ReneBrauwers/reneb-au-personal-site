using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages;

public sealed class RecruitersModel(PortalDatabase database) : PageModel
{
    public PublicCandidateProfile Profile { get; private set; } = new();
    public string JsonLd { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Profile = await database.GetPublicProfileAsync(false, cancellationToken);
        JsonLd = PublicProfileRenderer.ToJsonLd(Profile).Replace("</", "<\\/", StringComparison.Ordinal);
    }
}

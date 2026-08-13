using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Pages.Admin.Content;

[Authorize(Policy = "Admin")]
public sealed class IndexModel(PortalDatabase database) : PageModel
{
    public IReadOnlyList<ContentDocumentRecord> Documents { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken cancellationToken) => Documents = await database.ListContentDocumentsAsync(cancellationToken);
    public static string Label(string key) => key switch
    {
        ContentDocumentKeys.Home => "Homepage",
        ContentDocumentKeys.SiteSettings => "Global settings and Umami",
        ContentDocumentKeys.RecruiterProfile => "Public recruiter profile",
        ContentDocumentKeys.OpportunityProfile => "Private opportunity profile",
        ContentDocumentKeys.Privacy => "Privacy notice",
        ContentDocumentKeys.Discovery => "Machine-discovery guidance",
        _ => key
    };
}

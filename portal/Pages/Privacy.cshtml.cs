using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Pages;

public sealed class PrivacyModel(PortalDatabase database) : PageModel
{
    public PrivacyNoticeContent Notice { get; private set; } = ContentDefaults.Privacy();
    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Notice = (await database.GetContentAsync<PrivacyNoticeContent>(ContentDocumentKeys.Privacy, false, cancellationToken)).Content;
    public string Html(RichTextContent value) => RichTextDelta.ToHtml(value);
}

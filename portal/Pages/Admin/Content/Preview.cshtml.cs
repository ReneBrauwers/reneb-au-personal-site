using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Admin.Content;

[Authorize(Policy = "Admin")]
public sealed class PreviewModel(PortalDatabase database) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Key { get; set; } = string.Empty;
    public object Draft { get; private set; } = new();
    public string? LlmsText { get; private set; }
    public string? Markdown { get; private set; }
    public string? CandidateJson { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ContentDocumentKeys.All.Contains(Key, StringComparer.Ordinal)) return NotFound();
        Draft = ContentTypeRegistry.DeserializeAndValidate(Key, await database.GetContentJsonAsync(Key, true, cancellationToken));
        if (Key is ContentDocumentKeys.RecruiterProfile or ContentDocumentKeys.Discovery)
        {
            var profile = Key == ContentDocumentKeys.RecruiterProfile
                ? (PublicCandidateProfile)Draft
                : (await database.GetContentAsync<PublicCandidateProfile>(ContentDocumentKeys.RecruiterProfile, true, cancellationToken)).Content;
            var guidance = Key == ContentDocumentKeys.Discovery
                ? (DiscoveryGuidanceContent)Draft
                : (await database.GetContentAsync<DiscoveryGuidanceContent>(ContentDocumentKeys.Discovery, true, cancellationToken)).Content;
            LlmsText = PublicProfileRenderer.ToLlmsText(profile, guidance);
            Markdown = PublicProfileRenderer.ToMarkdown(profile, guidance);
            CandidateJson = PublicProfileRenderer.ToJson(profile, guidance);
        }
        return Page();
    }

    public static string Html(RichTextContent content) => RichTextDelta.ToHtml(content);
}

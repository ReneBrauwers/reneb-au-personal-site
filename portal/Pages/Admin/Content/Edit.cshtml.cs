using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Admin.Content;

[Authorize(Policy = "Admin")]
public sealed class EditModel(PortalDatabase database, IdentityService identity) : PageModel
{
    [BindProperty(SupportsGet = true)] public string Key { get; set; } = string.Empty;
    [BindProperty, Required] public string Json { get; set; } = string.Empty;
    [BindProperty] public long Revision { get; set; }
    public IReadOnlyList<ContentRevisionRecord> Revisions { get; private set; } = [];
    public IReadOnlyList<ContentDiffEntry> Diff { get; private set; } = [];
    public string DraftPreviewUrl => $"/admin/content/preview/{Uri.EscapeDataString(Key)}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!ContentDocumentKeys.All.Contains(Key, StringComparer.Ordinal)) return NotFound();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ContentDocumentKeys.All.Contains(Key, StringComparer.Ordinal)) return NotFound();
        if (!ModelState.IsValid) { await LoadMetaAsync(cancellationToken); return Page(); }
        try
        {
            var result = await database.SaveContentJsonDraftAsync(Key, Json, Revision, IdentityService.CurrentUserId(User), cancellationToken);
            TempData["Status"] = $"Draft revision {result.Revision} saved.";
            return Redirect($"/admin/content/{Key}");
        }
        catch (ValidationException exception) { ModelState.AddModelError(nameof(Json), exception.Message); }
        catch (JsonException) { ModelState.AddModelError(nameof(Json), "The content document is not valid JSON."); }
        catch (ContentConcurrencyException exception) { ModelState.AddModelError(nameof(Revision), $"The draft changed. Current revision: {exception.CurrentRevision}. Reload before applying your changes."); }
        await LoadMetaAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        if (!ContentDocumentKeys.All.Contains(Key, StringComparer.Ordinal)) return NotFound();
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect($"/admin/totp?returnUrl=/admin/content/{Uri.EscapeDataString(Key)}");
        try
        {
            await database.PublishContentJsonAsync(Key, Json, Revision, IdentityService.CurrentUserId(User), cancellationToken);
            TempData["Status"] = "The draft was published.";
            return Redirect($"/admin/content/{Key}");
        }
        catch (ContentConcurrencyException conflict)
        { ModelState.AddModelError(nameof(Revision), $"The draft changed. Current revision: {conflict.CurrentRevision}."); }
        catch (ValidationException exception) { ModelState.AddModelError(nameof(Json), exception.Message); }
        await LoadMetaAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRollbackAsync(Guid revisionId, CancellationToken cancellationToken)
    {
        if (!ContentDocumentKeys.All.Contains(Key, StringComparer.Ordinal)) return NotFound();
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect($"/admin/totp?returnUrl=/admin/content/{Uri.EscapeDataString(Key)}");
        await database.RollbackContentAsync(Key, revisionId, IdentityService.CurrentUserId(User), cancellationToken);
        TempData["Status"] = "The selected revision was restored and published as a new revision.";
        return Redirect($"/admin/content/{Key}");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Json = await database.GetContentJsonAsync(Key, true, cancellationToken);
        var documents = await database.ListContentDocumentsAsync(cancellationToken);
        Revision = documents.Single(item => item.Key == Key).DraftRevision;
        var draft = ContentTypeRegistry.DeserializeAndValidate(Key, Json);
        var published = ContentTypeRegistry.DeserializeAndValidate(Key, await database.GetContentJsonAsync(Key, false, cancellationToken));
        Diff = ContentDiffService.Compare(published, draft);
        await LoadMetaAsync(cancellationToken);
    }

    private async Task LoadMetaAsync(CancellationToken cancellationToken) => Revisions = await database.ListContentRevisionsAsync(Key, cancellationToken);
}

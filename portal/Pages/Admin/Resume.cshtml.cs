using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "Admin")]
[RequestFormLimits(MultipartBodyLengthLimit = PdfValidator.MaximumBytes + 65_536)]
[RequestSizeLimit(PdfValidator.MaximumBytes + 65_536)]
public sealed class ResumeModel(PortalDatabase database, PdfValidator validator, IdentityService identity) : PageModel
{
    [BindProperty] public IFormFile? Upload { get; set; }
    public ResumeRecord? Current { get; private set; }

    public async Task OnGetAsync(CancellationToken token) => Current = await database.GetActiveResumeAsync(token);

    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/resume");
        if (Upload is null)
        {
            ModelState.AddModelError(nameof(Upload), "Select a PDF to upload.");
            Current = await database.GetActiveResumeAsync(token);
            return Page();
        }
        var validation = await validator.ValidateAsync(Upload, token);
        if (!validation.Valid)
        {
            ModelState.AddModelError(nameof(Upload), validation.Error);
            Current = await database.GetActiveResumeAsync(token);
            return Page();
        }
        await using var stream = new MemoryStream(checked((int)Upload.Length));
        await Upload.CopyToAsync(stream, token);
        await database.SaveResumeAsync(Path.GetFileName(Upload.FileName), stream.ToArray(), IdentityService.CurrentUserId(User), token);
        TempData["Status"] = "The validated PDF is now the active résumé. Existing grants must be renewed for this version.";
        return RedirectToPage();
    }
}

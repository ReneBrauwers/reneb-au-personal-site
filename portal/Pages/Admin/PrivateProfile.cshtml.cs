using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed class PrivateProfileModel(PortalDatabase database, IdentityService identity) : PageModel
{
    [BindProperty] public PrivateCandidateProfile Input { get; set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken) => Input = await database.GetPrivateProfileAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/private-profile");
        if (!ModelState.IsValid) return Page();
        await database.SavePrivateProfileAsync(Input, IdentityService.CurrentUserId(User), cancellationToken);
        TempData["Status"] = "The encrypted opportunity profile was updated.";
        return RedirectToPage();
    }
}

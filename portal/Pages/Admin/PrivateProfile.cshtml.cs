using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed class PrivateProfileModel : PageModel
{
    [BindProperty] public PrivateCandidateProfile Input { get; set; } = new();

    public IActionResult OnGet() => Redirect("/admin/content/opportunity-profile");

    public IActionResult OnPost()
    {
        TempData["Status"] = "The opportunity profile now uses governed drafts and publishing in Content Studio.";
        return Redirect("/admin/content/opportunity-profile");
    }
}

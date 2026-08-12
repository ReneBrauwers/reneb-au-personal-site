using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Auth;

public sealed class LogoutModel(PortalDatabase database) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.FindFirst("session_id")?.Value is { Length: > 0 } sessionToken)
        {
            await database.RevokeSessionAsync(IdentityService.HashSession(sessionToken), HttpContext.RequestAborted);
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}

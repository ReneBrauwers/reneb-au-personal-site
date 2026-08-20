using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Auth;

[EnableRateLimiting("auth")]
public sealed class LoginModel(IdentityService identity) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = string.Empty;
    [BindProperty]
    public bool ReenrollAuthenticator { get; set; }
    public bool Submitted { get; private set; }
    public bool SubmittedReenrollment { get; private set; }
    public bool IsAdmin { get; private set; }

    public void OnGet(string mode) => IsAdmin = string.Equals(mode, "admin", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnPostAsync(string mode, CancellationToken cancellationToken)
    {
        IsAdmin = string.Equals(mode, "admin", StringComparison.OrdinalIgnoreCase);
        if (!ModelState.IsValid)
        {
            return Page();
        }
        SubmittedReenrollment = IsAdmin && ReenrollAuthenticator;
        await identity.RequestLoginAsync(Email, IsAdmin, SubmittedReenrollment, cancellationToken);
        Submitted = true;
        ModelState.Clear();
        return Page();
    }
}

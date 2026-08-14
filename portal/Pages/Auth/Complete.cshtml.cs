using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;

namespace ReneB.Portal.Pages.Auth;

[EnableRateLimiting("auth")]
public sealed class CompleteModel(IdentityService identity, PortalDatabase database, IOptions<PortalOptions> options) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Enter the email address that received the sign-in message.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;
    [BindProperty]
    [Required(ErrorMessage = "Enter the eight-character email verification code.")]
    [StringLength(8, MinimumLength = 8, ErrorMessage = "Enter the complete eight-character code from the email.")]
    public string Code { get; set; } = string.Empty;
    public string? ErrorMessage { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostTokenAsync(string token, CancellationToken cancellationToken)
        => await CompleteAsync(await identity.CompleteTokenAsync(token, cancellationToken, adminOnly: !options.Value.Enabled), cancellationToken);

    public async Task<IActionResult> OnPostCodeAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Enter the email address and complete eight-character email verification code from the message.";
            return Page();
        }
        return await CompleteAsync(await identity.CompleteCodeAsync(Email, Code, cancellationToken, adminOnly: !options.Value.Enabled), cancellationToken);
    }

    private async Task<IActionResult> CompleteAsync(RecruiterRecord? recruiter, CancellationToken cancellationToken)
    {
        if (recruiter is null || (!options.Value.Enabled && !identity.IsAdminEmail(recruiter.Email)))
        {
            ErrorMessage = "That verification has expired or has already been used. Request a new sign-in link.";
            return Page();
        }
        await identity.SignInAsync(HttpContext, recruiter);
        await database.TouchRecruiterAsync(recruiter.Id, cancellationToken);
        if (identity.IsAdminEmail(recruiter.Email))
        {
            return Redirect("/admin/totp");
        }
        if (recruiter.Status == RecruiterStatus.PendingApproval && options.Value.AdminEmails.FirstOrDefault() is { Length: > 0 } admin)
        {
            await database.EnqueueMailAsync("access-review", admin, "Recruiter access review requested on reneb.au",
                "<p>A verified recruiter registration requires review. Sign in to the administrator portal to approve or decline access.</p>", cancellationToken);
        }
        return recruiter.Status == RecruiterStatus.Active ? Redirect("/portal") : Redirect("/auth/pending");
    }
}

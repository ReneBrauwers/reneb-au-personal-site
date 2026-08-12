using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Auth;

[RequestFormLimits(ValueCountLimit = 50)]
[EnableRateLimiting("auth")]
public sealed class RegisterModel(IdentityService identity) : PageModel
{
    [BindProperty]
    public RegistrationInput Input { get; set; } = new();
    public bool Submitted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        await identity.RequestRegistrationAsync(new RecruiterRegistration(
            Input.Name, Input.Email, Input.Organisation, Input.Title, Input.ProfileUrl, Input.Country, Input.Phone, Input.Purpose), cancellationToken);
        Submitted = true;
        ModelState.Clear();
        return Page();
    }

    public sealed class RegistrationInput
    {
        [Required, StringLength(120), Display(Name = "Full name")]
        public string Name { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(254), Display(Name = "Work email")]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(160)]
        public string Organisation { get; set; } = string.Empty;

        [Required, StringLength(160), Display(Name = "Role or title")]
        public string Title { get; set; } = string.Empty;

        [Required, Url, StringLength(500)]
        public string ProfileUrl { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string Country { get; set; } = "Australia";

        [Phone, StringLength(50)]
        public string? Phone { get; set; }

        [Required, StringLength(2000, MinimumLength = 20)]
        public string Purpose { get; set; } = string.Empty;

        [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the privacy notice to continue.")]
        public bool PrivacyAccepted { get; set; }
    }
}

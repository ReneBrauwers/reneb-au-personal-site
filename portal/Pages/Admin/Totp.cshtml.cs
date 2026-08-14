using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using ReneB.Portal.Data;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "AdminBase")]
[EnableRateLimiting("auth")]
public sealed class TotpModel(PortalDatabase database, IdentityService identity, TimeProvider time, IDataProtectionProvider dataProtection) : PageModel
{
    internal const string EnrollmentProtectionPurpose = "reneb-au-totp-enrollment-v1";
    private readonly IDataProtector _enrollmentProtector = dataProtection.CreateProtector(EnrollmentProtectionPurpose);

    [BindProperty]
    [Required(ErrorMessage = "Enter the six-digit number from your authenticator app.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Enter the six-digit number from your authenticator app. The email code does not work on this step.")]
    public string Code { get; set; } = string.Empty;
    [BindProperty] public string? ReturnUrl { get; set; }
    [BindProperty] public string Enrollment { get; set; } = string.Empty;
    public string SecretBase32 { get; private set; } = string.Empty;
    public string AdminEmail { get; private set; } = string.Empty;
    public bool IsNew { get; private set; }

    public async Task OnGetAsync(string? returnUrl, CancellationToken token)
    {
        ReturnUrl = returnUrl;
        await LoadAsync(token);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken token)
    {
        var id = IdentityService.CurrentUserId(User);
        if (!await database.TryBeginAdminTotpAttemptAsync(id, cancellationToken: token))
        {
            ModelState.AddModelError(nameof(Code), "Too many verification attempts. Wait before trying again.");
            await LoadAsync(token);
            return Page();
        }
        if (!ModelState.IsValid)
        {
            await LoadAsync(token);
            return Page();
        }
        var secret = await database.GetAdminTotpSecretAsync(id, token);
        var enrolling = secret is null;
        if (enrolling)
        {
            try
            {
                secret = Convert.FromBase64String(_enrollmentProtector.Unprotect(Enrollment));
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException)
            {
                secret = null;
            }
        }
        if (secret is null || !TotpService.Validate(secret, Code, time.GetUtcNow()))
        {
            ModelState.AddModelError(nameof(Code), "That authenticator code is invalid or expired. Enter the current six-digit number shown in your authenticator app.");
            await LoadAsync(token);
            return Page();
        }
        if (enrolling)
        {
            await database.SaveAdminTotpSecretAsync(id, secret, token);
        }
        await database.ClearAdminTotpAttemptsAsync(id, token);
        var account = await database.GetRecruiterAsync(id, token) ?? throw new InvalidOperationException("Administrator account is missing.");
        await identity.SignInAsync(HttpContext, account, totpVerified: true);
        return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : Redirect("/admin");
    }

    private async Task LoadAsync(CancellationToken token)
    {
        var id = IdentityService.CurrentUserId(User);
        AdminEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var secret = await database.GetAdminTotpSecretAsync(id, token);
        if (secret is null)
        {
            try
            {
                secret = string.IsNullOrWhiteSpace(Enrollment)
                    ? null
                    : Convert.FromBase64String(_enrollmentProtector.Unprotect(Enrollment));
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException)
            {
                secret = null;
            }
            secret ??= TotpService.CreateSecret();
            IsNew = true;
            Enrollment = _enrollmentProtector.Protect(Convert.ToBase64String(secret));
            ModelState.Remove(nameof(Enrollment));
        }
        SecretBase32 = TotpService.ToBase32(secret);
    }
}

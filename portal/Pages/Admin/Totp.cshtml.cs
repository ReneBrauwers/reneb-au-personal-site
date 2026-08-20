using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;
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
    [BindProperty] public string? Enrollment { get; set; }
    public string SecretBase32 { get; private set; } = string.Empty;
    public string QrCodeDataUri { get; private set; } = string.Empty;
    public string AdminEmail { get; private set; } = string.Empty;
    public bool IsNew { get; private set; }
    public bool IsReenrollment { get; private set; }
    public bool IsEnrollment => IsNew || IsReenrollment;

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
        var existingCredential = await database.GetAdminTotpCredentialAsync(id, token);
        var existingTotpSeed = existingCredential?.Seed;
        var reenrolling = existingTotpSeed is not null && User.HasClaim(IdentityService.TotpReenrollmentClaim, bool.TrueString);
        var totpSeed = existingTotpSeed;
        var enrolling = totpSeed is null || reenrolling;
        if (enrolling)
        {
            try
            {
                totpSeed = Convert.FromBase64String(_enrollmentProtector.Unprotect(Enrollment ?? string.Empty));
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException)
            {
                totpSeed = null;
            }
        }
        if (totpSeed is null || !TotpService.Validate(totpSeed, Code, time.GetUtcNow()))
        {
            ModelState.AddModelError(nameof(Code), "That authenticator code is invalid or expired. Enter the current six-digit number shown in your authenticator app.");
            await LoadAsync(token);
            return Page();
        }
        long verifiedTotpVersion;
        if (enrolling)
        {
            verifiedTotpVersion = await database.CompleteAdminTotpEnrollmentAsync(id, totpSeed, reenrolling, token);
        }
        else
        {
            await database.ClearAdminTotpAttemptsAsync(id, token);
            verifiedTotpVersion = existingCredential?.Version
                ?? throw new InvalidOperationException("The verified administrator TOTP credential is missing.");
        }
        var account = await database.GetRecruiterAsync(id, token) ?? throw new InvalidOperationException("Administrator account is missing.");
        if (!await identity.SignInWithTotpAsync(HttpContext, account, verifiedTotpVersion))
        {
            return Redirect("/auth/admin");
        }
        return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl) : Redirect("/admin");
    }

    private async Task LoadAsync(CancellationToken token)
    {
        var id = IdentityService.CurrentUserId(User);
        AdminEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        var existingTotpSeed = await database.GetAdminTotpSecretAsync(id, token);
        IsNew = existingTotpSeed is null;
        IsReenrollment = existingTotpSeed is not null && User.HasClaim(IdentityService.TotpReenrollmentClaim, bool.TrueString);
        var totpSeed = existingTotpSeed;
        if (IsEnrollment)
        {
            try
            {
                totpSeed = string.IsNullOrWhiteSpace(Enrollment)
                    ? null
                    : Convert.FromBase64String(_enrollmentProtector.Unprotect(Enrollment ?? string.Empty));
            }
            catch (Exception exception) when (exception is CryptographicException or FormatException)
            {
                totpSeed = null;
            }
            totpSeed ??= TotpService.CreateSecret();
            Enrollment = _enrollmentProtector.Protect(Convert.ToBase64String(totpSeed));
            ModelState.Remove(nameof(Enrollment));
            SecretBase32 = TotpService.ToBase32(totpSeed);
            var label = Uri.EscapeDataString($"reneb.au:{AdminEmail}");
            var issuer = Uri.EscapeDataString("reneb.au");
            var otpAuthUri = $"otpauth://totp/{label}?secret={SecretBase32}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";
            var qrCode = PngByteQRCodeHelper.GetQRCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q, 8);
            QrCodeDataUri = $"data:image/png;base64,{Convert.ToBase64String(qrCode)}";
        }
    }
}

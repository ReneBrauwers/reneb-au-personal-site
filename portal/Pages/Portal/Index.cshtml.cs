using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Portal;

public sealed class IndexModel(
    PortalDatabase database,
    IOptions<PortalOptions> options,
    TimeProvider time,
    IdentityService identity,
    MandateLensService mandateLens) : PageModel
{
    public PrivateCandidateProfile Profile { get; private set; } = new();
    public bool ResumeAvailable { get; private set; }
    public bool GrantActive { get; private set; }
    public DateTimeOffset? GrantExpires { get; private set; }
    public bool CanDeleteAccount { get; private set; }
    public bool IsAdmin { get; private set; }
    public MandateLensResult? LensResult { get; private set; }

    [BindProperty, Required, StringLength(160)]
    public string MessageSubject { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(4000, MinimumLength = 20)]
    public string MessageBody { get; set; } = string.Empty;
    [BindProperty]
    public bool ConfirmDeletion { get; set; }
    [BindProperty, StringLength(MandateLensService.MaximumRoleLength)]
    public string? MandateRole { get; set; }
    [BindProperty, StringLength(MandateLensService.MaximumMandateLength)]
    public string? MandateText { get; set; }
    [BindProperty, StringLength(MandateLensService.MaximumNoteLength)]
    public string? MandateNote { get; set; }

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);

    public async Task<IActionResult> OnPostMessageAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadAsync(cancellationToken);
            return Page();
        }
        var id = IdentityService.CurrentUserId(User);
        await database.AddMessageAsync(id, MessageSubject, MessageBody, cancellationToken);
        await QueueAdministratorNotificationAsync(cancellationToken);
        TempData["Status"] = "Your message has been saved for René.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLensAsync(CancellationToken cancellationToken)
    {
        RemoveMessageValidation();
        if (!ValidateMandate())
        {
            await LoadAsync(cancellationToken);
            return Page();
        }

        LensResult = mandateLens.Analyse(MandateText!, await database.GetPublicProfileAsync(false, cancellationToken));
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostShareLensAsync(CancellationToken cancellationToken)
    {
        RemoveMessageValidation();
        if (!ValidateMandate())
        {
            if (HasAnalysableMandate())
            {
                LensResult = mandateLens.Analyse(MandateText!, await database.GetPublicProfileAsync(false, cancellationToken));
            }
            await LoadAsync(cancellationToken);
            return Page();
        }
        if (User.FindFirstValue(ClaimTypes.Email) is { } email && identity.IsAdminEmail(email))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var result = mandateLens.Analyse(MandateText!, await database.GetPublicProfileAsync(false, cancellationToken));
        var role = string.IsNullOrWhiteSpace(MandateRole) ? "private role brief" : MandateRole.Trim();
        var subject = $"Mandate Lens · {role}";
        if (subject.Length > 160)
        {
            subject = subject[..159] + "…";
        }
        await database.AddMessageAsync(
            IdentityService.CurrentUserId(User),
            subject,
            mandateLens.ComposePrivateMessage(MandateRole, MandateText!, MandateNote, result),
            cancellationToken);
        await QueueAdministratorNotificationAsync(cancellationToken);
        TempData["Status"] = "Your Mandate Lens brief was encrypted and shared privately with René.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRequestResumeAsync(CancellationToken cancellationToken)
    {
        var id = IdentityService.CurrentUserId(User);
        if (!await database.TryQueueResumeAccessRequestAsync(id, options.Value.AdminEmails.FirstOrDefault(), cancellationToken))
        {
            TempData["Status"] = "Your résumé request is already queued for review.";
            return RedirectToPage();
        }
        TempData["Status"] = "Your résumé request has been sent for review.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAccountAsync(CancellationToken cancellationToken)
    {
        if (!ConfirmDeletion)
        {
            ModelState.AddModelError(nameof(ConfirmDeletion), "Confirm deletion before continuing.");
            await LoadAsync(cancellationToken);
            return Page();
        }
        var id = IdentityService.CurrentUserId(User);
        if (User.FindFirstValue(ClaimTypes.Email) is { } email && identity.IsAdminEmail(email))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }
        await database.DeleteRecruiterAsync(id, id, cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var id = IdentityService.CurrentUserId(User);
        Profile = await database.GetPrivateProfileAsync(cancellationToken);
        var resume = await database.GetActiveResumeAsync(cancellationToken);
        var grant = await database.GetResumeGrantAsync(id, cancellationToken);
        ResumeAvailable = resume is not null;
        GrantActive = resume is not null && grant is { RevokedAt: null } && grant.ResumeId == resume.Id && grant.ExpiresAt > time.GetUtcNow();
        GrantExpires = GrantActive ? grant!.ExpiresAt : null;
        IsAdmin = User.FindFirstValue(ClaimTypes.Email) is { } email && identity.IsAdminEmail(email);
        CanDeleteAccount = !IsAdmin;
        await database.TouchRecruiterAsync(id, cancellationToken);
    }

    private bool ValidateMandate()
    {
        var text = MandateText?.Trim() ?? string.Empty;
        if (text.Length < 100)
        {
            ModelState.AddModelError(nameof(MandateText), "Paste at least 100 characters so the lens has enough mandate context to test.");
        }
        return ModelState.IsValid;
    }

    private bool HasAnalysableMandate()
    {
        var length = MandateText?.Trim().Length ?? 0;
        return length >= 100 && length <= MandateLensService.MaximumMandateLength;
    }

    private void RemoveMessageValidation()
    {
        ModelState.Remove(nameof(MessageSubject));
        ModelState.Remove(nameof(MessageBody));
    }

    private async Task QueueAdministratorNotificationAsync(CancellationToken cancellationToken)
    {
        var admin = options.Value.AdminEmails.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(admin))
        {
            await database.EnqueueMailAsync("new-message", admin, "New private recruiter message on reneb.au",
                "<p>A verified recruiter left a private message. Sign in to the administrator inbox to review it. The message body is deliberately omitted from email.</p>", cancellationToken);
        }
    }

    public string Html(RichTextContent value) => RichTextDelta.ToHtml(value);
    public string HtmlOrFallback(RichTextContent value)
        => string.IsNullOrWhiteSpace(RichTextDelta.ToPlainText(value)) ? "<p>Not currently disclosed.</p>" : RichTextDelta.ToHtml(value);
}

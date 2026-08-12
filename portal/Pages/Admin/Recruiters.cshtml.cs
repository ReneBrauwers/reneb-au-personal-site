using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed class RecruitersModel(PortalDatabase database, IdentityService identity) : PageModel
{
    public List<RecruiterRecord> Recruiters { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken cancellationToken) => Recruiters = await database.ListRecruitersAsync(cancellationToken);
    public Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken token) => ChangeStatusAsync(id, RecruiterStatus.Active, "Recruiter access approved.", token);
    public Task<IActionResult> OnPostSuspendAsync(Guid id, CancellationToken token) => ChangeStatusAsync(id, RecruiterStatus.Suspended, "Recruiter access suspended.", token);

    public async Task<IActionResult> OnPostGrantAsync(Guid id, CancellationToken token)
    {
        if (!Recent()) return StepUp();
        var recruiter = await database.GetRecruiterAsync(id, token);
        if (recruiter is null) return NotFound();
        if (recruiter.Status != RecruiterStatus.Active)
        {
            TempData["Status"] = "Résumé access can be granted only after recruiter access is active.";
            return RedirectToPage();
        }
        try
        {
            await database.GrantResumeAsync(id, IdentityService.CurrentUserId(User), token);
        }
        catch (InvalidOperationException exception)
        {
            TempData["Status"] = exception.Message;
            return RedirectToPage();
        }
        await database.EnqueueMailAsync("resume-granted", recruiter.Email, "Your reneb.au résumé access was approved",
            "<p>Your verified recruiter account now has a revocable 30-day résumé download grant. Sign in to the portal to download the current version.</p>", token);
        TempData["Status"] = "A revocable 30-day résumé grant was created.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostRevokeAsync(Guid id, CancellationToken token)
    {
        if (!Recent()) return StepUp();
        var recruiter = await database.GetRecruiterAsync(id, token);
        if (recruiter is null) return NotFound();
        await database.RevokeResumeAsync(id, IdentityService.CurrentUserId(User), token);
        await database.EnqueueMailAsync("resume-revoked", recruiter.Email, "Your reneb.au résumé access changed",
            "<p>Your résumé download grant is no longer active. Your verified portal account remains available while the underlying recruiter access is active.</p>", token);
        TempData["Status"] = "Résumé access was revoked.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken token)
    {
        if (!Recent()) return StepUp();
        var recruiter = await database.GetRecruiterAsync(id, token);
        if (recruiter is null) return NotFound();
        await database.DeleteRecruiterAsync(id, IdentityService.CurrentUserId(User), token);
        TempData["Status"] = "The recruiter account and private content were deleted.";
        return RedirectToPage();
    }
    private async Task<IActionResult> ChangeStatusAsync(Guid id, RecruiterStatus status, string message, CancellationToken token)
    {
        if (!Recent()) return StepUp();
        var recruiter = await database.GetRecruiterAsync(id, token);
        if (recruiter is null) return NotFound();
        await database.SetRecruiterStatusAsync(id, status, IdentityService.CurrentUserId(User), token);
        if (status == RecruiterStatus.Suspended)
        {
            await database.RevokeResumeAsync(id, IdentityService.CurrentUserId(User), token);
        }
        await database.EnqueueMailAsync("access-changed", recruiter.Email,
            status == RecruiterStatus.Active ? "Your reneb.au recruiter access was approved" : "Your reneb.au recruiter access was suspended",
            status == RecruiterStatus.Active
                ? "<p>Your verified recruiter account can now view René's private opportunity criteria and leave messages. Résumé access remains separately approval-controlled.</p>"
                : "<p>Your recruiter portal access is no longer active. Any résumé grant has also been revoked.</p>", token);
        TempData["Status"] = message;
        return RedirectToPage();
    }
    private bool Recent() => identity.HasRecentTotp(User, TimeSpan.FromMinutes(5));
    private RedirectResult StepUp() => Redirect("/admin/totp?returnUrl=/admin/recruiters");
}

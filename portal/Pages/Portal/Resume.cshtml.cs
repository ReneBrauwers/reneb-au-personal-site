using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Portal;

public sealed class ResumeModel(PortalDatabase database) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var id = IdentityService.CurrentUserId(User);
        var resume = await database.GetResumeForRecruiterAsync(id, cancellationToken);
        if (resume is null)
        {
            return NotFound();
        }
        await database.RecordResumeDownloadAsync(id, resume.Value.Record.Id, cancellationToken);
        Response.Headers["Cache-Control"] = "no-store";
        return new FileContentResult(resume.Value.Content, "application/pdf")
        {
            FileDownloadName = "rene-brauwers-resume.pdf",
            EnableRangeProcessing = false
        };
    }
}

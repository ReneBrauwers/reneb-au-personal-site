using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed class MessagesModel(PortalDatabase database, IdentityService identity) : PageModel
{
    public List<MessageRecord> Messages { get; private set; } = [];
    public async Task OnGetAsync(CancellationToken token) => Messages = await database.ListMessagesAsync(token);
    public async Task<IActionResult> OnPostReadAsync(Guid id, CancellationToken token)
    {
        await database.MarkMessageReadAsync(id, IdentityService.CurrentUserId(User), token);
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(Guid id, CancellationToken token)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/messages");
        await database.DeleteMessageAsync(id, IdentityService.CurrentUserId(User), token);
        TempData["Status"] = "The message content was permanently deleted.";
        return RedirectToPage();
    }
}

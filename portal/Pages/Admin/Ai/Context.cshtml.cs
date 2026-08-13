using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Admin.Ai;
[Authorize(Policy="Admin")]
[RequestFormLimits(MultipartBodyLengthLimit=10*1024*1024+65536)]
[RequestSizeLimit(10*1024*1024+65536)]
public sealed class ContextModel(PortalDatabase database,AiContextExtractor extractor,IdentityService identity,IOptions<AiOptions> options):PageModel
{
 [BindProperty] public IFormFile? Upload{get;set;} public IReadOnlyList<AiContextAssetRecord> Assets{get;private set;}=[];public long TotalBytes{get;private set;}
 public async Task OnGetAsync(CancellationToken token)=>await LoadAsync(token);
 public async Task<IActionResult> OnPostAsync(CancellationToken token){if(!identity.HasRecentTotp(User,TimeSpan.FromMinutes(5)))return Redirect("/admin/totp?returnUrl=/admin/ai/context");if(Upload is null){ModelState.AddModelError(nameof(Upload),"Select a context document.");await LoadAsync(token);return Page();}if(await database.GetAiContextLibrarySizeAsync(token)+Upload.Length>options.Value.MaximumContextLibraryBytes){ModelState.AddModelError(nameof(Upload),"The encrypted context library would exceed its 50 MB limit.");await LoadAsync(token);return Page();}var result=await extractor.ExtractAsync(Upload,token);if(!result.Valid){ModelState.AddModelError(nameof(Upload),result.Error);await LoadAsync(token);return Page();}await database.SaveAiContextAssetAsync(Path.GetFileName(Upload.FileName),result.MediaType,result.Content,result.ExtractedText,IdentityService.CurrentUserId(User),token);TempData["Status"]="The validated document and extracted text were encrypted into the context library.";return RedirectToPage();}
 public async Task<IActionResult> OnPostDeleteAsync(Guid id,CancellationToken token){if(!identity.HasRecentTotp(User,TimeSpan.FromMinutes(5)))return Redirect("/admin/totp?returnUrl=/admin/ai/context");await database.DeleteAiContextAssetAsync(id,IdentityService.CurrentUserId(User),token);TempData["Status"]="Context document permanently deleted.";return RedirectToPage();}
 private async Task LoadAsync(CancellationToken token){Assets=await database.ListAiContextAssetsAsync(token);TotalBytes=await database.GetAiContextLibrarySizeAsync(token);}
}

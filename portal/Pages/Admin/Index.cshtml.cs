using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace ReneB.Portal.Pages.Admin;
[Authorize(Policy = "Admin")]
public sealed class IndexModel : PageModel { public void OnGet() { } }

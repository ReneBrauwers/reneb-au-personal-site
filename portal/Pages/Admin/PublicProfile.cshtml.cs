using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Pages.Admin;

[Authorize(Policy = "Admin")]
public sealed class PublicProfileModel(PortalDatabase database, IdentityService identity) : PageModel
{
    [BindProperty]
    public PublicProfileInput Input { get; set; } = new();

    public PublicCandidateProfile? PreviewProfile { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
        => Input = PublicProfileInput.From(await database.GetPublicProfileAsync(true, cancellationToken));

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        await database.SavePublicDraftAsync(Input.ToProfile(), IdentityService.CurrentUserId(User), cancellationToken);
        TempData["Status"] = "The public profile draft was saved.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPreviewAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return Page();
        PreviewProfile = Input.ToProfile();
        await database.SavePublicDraftAsync(PreviewProfile, IdentityService.CurrentUserId(User), cancellationToken);
        ViewData["Title"] = "Preview public recruiter profile";
        return Page();
    }

    public async Task<IActionResult> OnPostPublishAsync(CancellationToken cancellationToken)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5)))
        {
            return Redirect("/admin/totp?returnUrl=/admin/public-profile");
        }
        if (!ModelState.IsValid) return Page();
        var actor = IdentityService.CurrentUserId(User);
        await database.PublishPublicProfileAsync(Input.ToProfile(), actor, cancellationToken);
        TempData["Status"] = "The draft was atomically published to every public recruiter representation.";
        return RedirectToPage();
    }

    public sealed class PublicProfileInput
    {
        [Required, StringLength(100)] public string CandidateName { get; set; } = string.Empty;
        [Required, StringLength(120)] public string Headline { get; set; } = string.Empty;
        [Required, StringLength(120)] public string CurrentRole { get; set; } = string.Empty;
        [Required, StringLength(160)] public string CurrentEmployer { get; set; } = string.Empty;
        [Required, StringLength(160)] public string ProfessionalContext { get; set; } = string.Empty;
        [Required, StringLength(900)] public string Summary { get; set; } = string.Empty;
        [Required, StringLength(4000)] public string DemonstratedSignals { get; set; } = string.Empty;
        [Required, StringLength(4000)] public string RolesOfInterest { get; set; } = string.Empty;
        [Required, StringLength(4000)] public string AreasOfInterest { get; set; } = string.Empty;
        [Required, StringLength(2000)] public string LocationPreferences { get; set; } = string.Empty;
        [Required, StringLength(2000)] public string StrongFitSignals { get; set; } = string.Empty;
        [Required, StringLength(2000)] public string PoorFitSignals { get; set; } = string.Empty;
        [Required, DataType(DataType.Date)] public DateOnly LastReviewed { get; set; }

        public PublicCandidateProfile ToProfile() => new()
        {
            CandidateName = CandidateName.Trim(), Headline = Headline.Trim(), CurrentRole = CurrentRole.Trim(), CurrentEmployer = CurrentEmployer.Trim(),
            ProfessionalContext = ProfessionalContext.Trim(), Summary = Summary.Trim(), LastReviewed = LastReviewed,
            DemonstratedSignals = Lines(DemonstratedSignals), RolesOfInterest = Lines(RolesOfInterest), AreasOfInterest = Lines(AreasOfInterest),
            LocationPreferences = Lines(LocationPreferences), StrongFitSignals = Lines(StrongFitSignals), PoorFitSignals = Lines(PoorFitSignals)
        };

        public static PublicProfileInput From(PublicCandidateProfile profile) => new()
        {
            CandidateName = profile.CandidateName, Headline = profile.Headline, CurrentRole = profile.CurrentRole, CurrentEmployer = profile.CurrentEmployer,
            ProfessionalContext = profile.ProfessionalContext, Summary = profile.Summary, LastReviewed = profile.LastReviewed,
            DemonstratedSignals = Join(profile.DemonstratedSignals), RolesOfInterest = Join(profile.RolesOfInterest), AreasOfInterest = Join(profile.AreasOfInterest),
            LocationPreferences = Join(profile.LocationPreferences), StrongFitSignals = Join(profile.StrongFitSignals), PoorFitSignals = Join(profile.PoorFitSignals)
        };

        private static List<string> Lines(string value) => value.Split(['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        private static string Join(IEnumerable<string> values) => string.Join(Environment.NewLine, values);
    }
}

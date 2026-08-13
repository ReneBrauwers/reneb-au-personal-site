using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Admin.Ai;

[Authorize(Policy = "Admin")]
public sealed class ProvidersModel(PortalDatabase database, AiAuthoringService authoring, IdentityService identity) : PageModel
{
    [BindProperty, Required] public AiProviderKind Provider { get; set; }
    [BindProperty, StringLength(500)] public string ApiKey { get; set; } = string.Empty;
    [BindProperty, StringLength(200)] public string ModelId { get; set; } = string.Empty;
    [BindProperty, Range(typeof(decimal), "0.01", "10000")] public decimal MonthlyBudgetUsd { get; set; } = 10;
    [BindProperty, Range(128, 32768)] public int MaximumOutputTokens { get; set; } = 4000;
    [BindProperty, Range(typeof(decimal), "0.01", "10000")] public decimal GlobalMonthlyBudgetUsd { get; set; } = 20;
    public IReadOnlyList<AiProviderConfigurationRecord> Configurations { get; private set; } = [];
    public IReadOnlyDictionary<AiProviderKind, IReadOnlyList<AiModelOption>> Models { get; private set; } = new Dictionary<AiProviderKind, IReadOnlyList<AiModelOption>>();
    public IReadOnlyDictionary<AiProviderKind, AiBudgetStatus> Budgets { get; private set; } = new Dictionary<AiProviderKind, AiBudgetStatus>();
    public bool EgressEnabled => authoring.EgressEnabled;
    public AiBudgetStatus GlobalBudget { get; private set; } = new(0, 0, 0, 0, string.Empty);

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadAsync(cancellationToken);
    public async Task<IActionResult> OnPostKeyAsync(CancellationToken cancellationToken)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/ai/providers");
        if (string.IsNullOrWhiteSpace(ApiKey)) ModelState.AddModelError(nameof(ApiKey), "Enter the provider API key.");
        if (!ModelState.IsValid) { await LoadAsync(cancellationToken); return Page(); }
        await database.SaveAiProviderKeyAsync(Provider, ApiKey, IdentityService.CurrentUserId(User), cancellationToken);
        TempData["Status"] = $"{Provider} key stored using the dedicated AI credential keyring. It must be tested before use."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostRefreshAsync(CancellationToken cancellationToken)
    {
        try { var models = await authoring.RefreshModelsAsync(Provider, cancellationToken); TempData["Status"] = $"Found {models.Count} compatible {Provider} models."; }
        catch (Exception exception) when (exception is InvalidOperationException or AiProviderException) { TempData["Error"] = exception is AiProviderException providerError ? $"Model discovery failed: {providerError.Code}." : exception.Message; }
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostSettingsAsync(CancellationToken cancellationToken)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/ai/providers");
        if (!ModelState.IsValid) { await LoadAsync(cancellationToken); return Page(); }
        await database.SaveAiProviderSettingsAsync(Provider, ModelId, MonthlyBudgetUsd, MaximumOutputTokens, IdentityService.CurrentUserId(User), cancellationToken);
        TempData["Status"] = "Provider model and cost controls saved. Run the connection test to enable authoring."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostTestAsync(CancellationToken cancellationToken)
    {
        var result = await authoring.TestProviderAsync(Provider, IdentityService.CurrentUserId(User), cancellationToken);
        TempData[result.Success ? "Status" : "Error"] = result.Success ? $"{Provider} passed authenticated structured-output testing. Observed ZDR: {result.ZeroDataRetentionObserved?.ToString() ?? "not reported"}." : $"Connection test failed: {result.ErrorCode}.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostGlobalBudgetAsync(CancellationToken cancellationToken)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/ai/providers");
        if (!ModelState.IsValid) { await LoadAsync(cancellationToken); return Page(); }
        await database.SaveAiGlobalBudgetAsync(GlobalMonthlyBudgetUsd, IdentityService.CurrentUserId(User), cancellationToken);
        TempData["Status"] = "The site-wide monthly AI ceiling was updated.";
        return RedirectToPage();
    }
    public async Task<IActionResult> OnPostDeleteAsync(CancellationToken cancellationToken)
    {
        if (!identity.HasRecentTotp(User, TimeSpan.FromMinutes(5))) return Redirect("/admin/totp?returnUrl=/admin/ai/providers");
        await database.DeleteAiProviderAsync(Provider, IdentityService.CurrentUserId(User), cancellationToken); TempData["Status"] = $"{Provider} configuration and encrypted key deleted."; return RedirectToPage();
    }
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Configurations = await database.ListAiProvidersAsync(cancellationToken);
        var models = new Dictionary<AiProviderKind, IReadOnlyList<AiModelOption>>(); var budgets = new Dictionary<AiProviderKind, AiBudgetStatus>();
        foreach (var provider in Enum.GetValues<AiProviderKind>()) { models[provider] = await database.GetAiModelsAsync(provider, allowExpired: true, cancellationToken); budgets[provider] = await database.GetAiBudgetStatusAsync(provider, cancellationToken); }
        Models = models; Budgets = budgets;
        GlobalBudget = await database.GetAiGlobalBudgetStatusAsync(cancellationToken);
        GlobalMonthlyBudgetUsd = GlobalBudget.BudgetUsd;
    }
}

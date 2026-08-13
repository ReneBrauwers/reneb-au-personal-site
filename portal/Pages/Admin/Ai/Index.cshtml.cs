using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Pages.Admin.Ai;

[Authorize(Policy="Admin")]
public sealed class IndexModel(PortalDatabase database,AiAuthoringService authoring):PageModel
{
 [BindProperty] public Guid? ConversationId{get;set;} [BindProperty,Required] public AiProviderKind Provider{get;set;} [BindProperty,Required] public string DocumentKey{get;set;}=ContentDocumentKeys.Home;
 [BindProperty,Required,StringLength(4000)] public string AuthoringRequest{get;set;}=string.Empty; [BindProperty] public bool IncludePublishedContent{get;set;}=true; [BindProperty] public bool IncludePrivateOpportunity{get;set;}
 [BindProperty] public bool IncludeResume{get;set;}
 [BindProperty] public List<Guid> ContextIds{get;set;}=[]; [BindProperty] public bool PrivateDisclosureAcknowledged{get;set;}
 public IReadOnlyList<AiProviderConfigurationRecord> Providers{get;private set;}=[]; public IReadOnlyList<AiContextAssetRecord> Assets{get;private set;}=[];public IReadOnlyList<AiConversationRecord> Conversations{get;private set;}=[];public IReadOnlyList<AiMessageRecord> Messages{get;private set;}=[];public AiProposalRecord? Proposal{get;private set;} public bool EgressEnabled=>authoring.EgressEnabled; public bool ResumeAvailable{get;private set;}
 public async Task<IActionResult> OnGetAsync(Guid? conversationId,Guid? proposalId,CancellationToken token){ConversationId=conversationId;if(proposalId is not null)Proposal=await database.GetAiProposalAsync(proposalId.Value,token);await LoadAsync(token);return Page();}
 public async Task<IActionResult> OnPostProposeAsync(CancellationToken token){try{var proposal=await authoring.ProposeAsync(ConversationId,Provider,DocumentKey,AuthoringRequest,ContextIds,IncludePublishedContent,IncludePrivateOpportunity,IncludeResume,PrivateDisclosureAcknowledged,IdentityService.CurrentUserId(User),token);return Redirect($"/admin/ai?conversationId={proposal.ConversationId}&proposalId={proposal.Id}");}catch(Exception ex)when(ex is ValidationException or InvalidOperationException or AiProviderException or ContentConcurrencyException){ModelState.AddModelError(string.Empty,ex is AiProviderException providerError?$"Provider request failed: {providerError.Code}.":ex.Message);await LoadAsync(token);return Page();}}
 public async Task<IActionResult> OnPostApplyAsync(Guid proposalId,CancellationToken token){await authoring.ApplyProposalAsync(proposalId,IdentityService.CurrentUserId(User),token);var proposal=await database.GetAiProposalAsync(proposalId,token);TempData["Status"]="The AI proposal was validated and applied to the draft. It has not been published.";return Redirect($"/admin/content/{proposal!.DocumentKey}");}
 public async Task<IActionResult> OnPostDeleteAsync(Guid conversationId,CancellationToken token){await database.DeleteAiConversationAsync(conversationId,IdentityService.CurrentUserId(User),token);TempData["Status"]="The encrypted conversation and proposals were deleted.";return RedirectToPage();}
 private async Task LoadAsync(CancellationToken token){Providers=(await database.ListAiProvidersAsync(token)).Where(item=>item.Ready).ToArray();Assets=await database.ListAiContextAssetsAsync(token);Conversations=await database.ListAiConversationsAsync(token);ResumeAvailable=await database.GetActiveResumeAsync(token) is not null;if(ConversationId is not null){Messages=await database.ListAiMessagesAsync(ConversationId.Value,token);var conversation=await database.GetAiConversationAsync(ConversationId.Value,token);if(conversation is not null){Provider=conversation.Provider;DocumentKey=conversation.DocumentKey;}}}
}

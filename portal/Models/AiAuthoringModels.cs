using System.ComponentModel.DataAnnotations;

namespace ReneB.Portal.Models;

public enum AiProviderKind { OpenRouter, Xai }
public enum AiProviderStatus { NotConfigured, Untested, Ready, Degraded, Disabled }
public enum AiProposalStatus { Proposed, Applied, Rejected }

public sealed record AiModelOption(
    string Id,
    string Name,
    int? ContextLength,
    decimal? PromptUsdPerToken,
    decimal? CompletionUsdPerToken,
    bool SupportsStructuredOutput,
    bool SupportsTextInput,
    bool SupportsTextOutput);

public sealed record AiProviderConfigurationRecord(
    AiProviderKind Provider,
    string KeyFingerprint,
    string KeyLastFour,
    string? SelectedModel,
    decimal MonthlyBudgetUsd,
    int MaximumOutputTokens,
    AiProviderStatus Status,
    DateTimeOffset? LastTestedAt,
    string? LastErrorCode,
    bool? ZeroDataRetentionObserved,
    DateTimeOffset UpdatedAt)
{
    public bool Ready => Status == AiProviderStatus.Ready && !string.IsNullOrWhiteSpace(SelectedModel) && MonthlyBudgetUsd > 0 && MaximumOutputTokens > 0;
}

public sealed record AiProviderSecret(AiProviderConfigurationRecord Configuration, string ApiKey);
public sealed record AiConnectionTestResult(bool Success, string? ErrorCode, bool? ZeroDataRetentionObserved, long InputTokens, long OutputTokens, decimal? CostUsd);
public sealed record AiAuthoringResult(string ProposalJson, long InputTokens, long OutputTokens, decimal? CostUsd, bool? ZeroDataRetentionObserved, string ProviderRequestId);

public sealed class ContentProposalDocument
{
    [Required, StringLength(120)] public string Summary { get; set; } = string.Empty;
    [Required] public string ContentJson { get; set; } = string.Empty;
}

public sealed record AiContextAssetRecord(Guid Id, string FileName, string MediaType, long Size, string Sha256, DateTimeOffset CreatedAt);
public sealed record AiConversationRecord(Guid Id, string DocumentKey, AiProviderKind Provider, string ModelId, DateTimeOffset CreatedAt, DateTimeOffset LastActiveAt);
public sealed record AiMessageRecord(Guid Id, Guid ConversationId, string Role, string Body, DateTimeOffset CreatedAt);
public sealed record AiProposalRecord(Guid Id, Guid ConversationId, string DocumentKey, long BaseRevision, string Summary, string ProposedContentJson, AiProposalStatus Status, DateTimeOffset CreatedAt);
public sealed record AiBudgetStatus(decimal BudgetUsd, decimal UsedUsd, decimal ReservedUsd, decimal RemainingUsd, string Period);

public sealed class AiAuthoringRequest
{
    public required string SystemInstructions { get; init; }
    public required string UserRequest { get; init; }
    public required string CurrentContentJson { get; init; }
    public required string ContentSchemaDescription { get; init; }
    public required IReadOnlyList<(string Name, string Text, bool Private)> Context { get; init; }
    public required int MaximumOutputTokens { get; init; }
}

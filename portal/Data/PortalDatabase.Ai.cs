using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Data;

public sealed partial class PortalDatabase
{
    public async Task SaveAiProviderKeyAsync(AiProviderKind provider, string apiKey, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length is < 16 or > 500) throw new ArgumentException("API key length is invalid.", nameof(apiKey));
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO AiProviderConfigurations
                (Provider, ApiKeyEncrypted, KeyFingerprint, KeyLastFour, SelectedModel, MonthlyBudgetUsd, MaximumOutputTokens, Status, UpdatedAt)
            VALUES ($provider, $key, $fingerprint, $lastFour, NULL, 0, 1000, 'Untested', $now)
            ON CONFLICT(Provider) DO UPDATE SET
                ApiKeyEncrypted = excluded.ApiKeyEncrypted, KeyFingerprint = excluded.KeyFingerprint, KeyLastFour = excluded.KeyLastFour,
                SelectedModel = NULL, Status = 'Untested', LastTestedAt = NULL, LastErrorCode = NULL, ZeroDataRetentionObserved = NULL, UpdatedAt = excluded.UpdatedAt;
            """, cancellationToken, ("$provider", provider.ToString()), ("$key", _aiCredentials.Encrypt(apiKey.Trim())),
            ("$fingerprint", AiCredentialEncryptionService.Fingerprint(apiKey.Trim())), ("$lastFour", apiKey.Trim()[^4..]), ("$now", Format(now)));
        await ExecuteAsync(connection, "DELETE FROM AiModelCache WHERE Provider = $provider;", cancellationToken, ("$provider", provider.ToString()));
        await InsertAuditAsync(connection, actorId, "ai_provider.key_replaced", provider.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteAiProviderAsync(AiProviderKind provider, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM AiProviderConfigurations WHERE Provider = $provider;", cancellationToken, ("$provider", provider.ToString()));
        await ExecuteAsync(connection, "DELETE FROM AiModelCache WHERE Provider = $provider;", cancellationToken, ("$provider", provider.ToString()));
        await InsertAuditAsync(connection, actorId, "ai_provider.deleted", provider.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AiProviderSecret?> GetAiProviderSecretAsync(AiProviderKind provider, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AiProviderConfigurations WHERE Provider = $provider;";
        command.Parameters.AddWithValue("$provider", provider.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new AiProviderSecret(ReadAiProvider(reader), _aiCredentials.Decrypt(reader.GetString(reader.GetOrdinal("ApiKeyEncrypted"))));
    }

    public async Task<IReadOnlyList<AiProviderConfigurationRecord>> ListAiProvidersAsync(CancellationToken cancellationToken = default)
    {
        var values = new List<AiProviderConfigurationRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT * FROM AiProviderConfigurations ORDER BY Provider;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(ReadAiProvider(reader));
        return values;
    }

    public async Task SaveAiProviderSettingsAsync(AiProviderKind provider, string modelId, decimal monthlyBudgetUsd, int maximumOutputTokens, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(modelId) || modelId.Length > 200) throw new ArgumentException("Select a valid model.");
        if (monthlyBudgetUsd is <= 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(monthlyBudgetUsd));
        if (maximumOutputTokens is < 128 or > 32_768) throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            UPDATE AiProviderConfigurations SET SelectedModel = $model, MonthlyBudgetUsd = $budget, MaximumOutputTokens = $tokens,
                Status = 'Untested', LastTestedAt = NULL, LastErrorCode = NULL, ZeroDataRetentionObserved = NULL, UpdatedAt = $now
            WHERE Provider = $provider;
            """, cancellationToken, ("$model", modelId), ("$budget", monthlyBudgetUsd), ("$tokens", maximumOutputTokens),
            ("$now", Format(_time.GetUtcNow())), ("$provider", provider.ToString()));
        await InsertAuditAsync(connection, actorId, "ai_provider.settings_updated", provider.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordAiConnectionTestAsync(AiProviderKind provider, AiConnectionTestResult result, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            UPDATE AiProviderConfigurations SET Status = $status, LastTestedAt = $now, LastErrorCode = $error,
                ZeroDataRetentionObserved = $zdr, UpdatedAt = $now WHERE Provider = $provider;
            """, cancellationToken, ("$status", result.Success
                ? AiProviderStatus.Ready.ToString()
                : result.ErrorCode == "authentication" ? AiProviderStatus.Disabled.ToString() : AiProviderStatus.Degraded.ToString()),
            ("$now", Format(_time.GetUtcNow())), ("$error", result.ErrorCode), ("$zdr", result.ZeroDataRetentionObserved is null ? null : result.ZeroDataRetentionObserved.Value ? 1 : 0),
            ("$provider", provider.ToString()));
        await InsertAuditAsync(connection, actorId, result.Success ? "ai_provider.test_passed" : "ai_provider.test_failed", provider.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RecordAiRetentionObservationAsync(AiProviderKind provider, bool? observed, CancellationToken cancellationToken = default)
    {
        if (observed is null) return;
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE AiProviderConfigurations SET ZeroDataRetentionObserved=$zdr, UpdatedAt=$now WHERE Provider=$provider;", cancellationToken,
            ("$zdr", observed.Value ? 1 : 0), ("$now", Format(_time.GetUtcNow())), ("$provider", provider.ToString()));
    }

    public async Task SaveAiModelsAsync(AiProviderKind provider, IReadOnlyList<AiModelOption> models, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO AiModelCache (Provider, ModelsJson, FetchedAt, ExpiresAt) VALUES ($provider, $models, $now, $expires)
            ON CONFLICT(Provider) DO UPDATE SET ModelsJson = excluded.ModelsJson, FetchedAt = excluded.FetchedAt, ExpiresAt = excluded.ExpiresAt;
            """, cancellationToken, ("$provider", provider.ToString()), ("$models", JsonSerializer.Serialize(models, ContentTypeRegistry.JsonOptions)),
            ("$now", Format(now)), ("$expires", Format(now.AddHours(1))));
        await using var selected = connection.CreateCommand();
        selected.CommandText = "SELECT SelectedModel FROM AiProviderConfigurations WHERE Provider=$provider;";
        selected.Parameters.AddWithValue("$provider", provider.ToString());
        var selectedModel = (string?)await selected.ExecuteScalarAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(selectedModel) && !models.Any(item => string.Equals(item.Id, selectedModel, StringComparison.Ordinal)))
        {
            await ExecuteAsync(connection, "UPDATE AiProviderConfigurations SET Status='Disabled', LastErrorCode='model_removed', UpdatedAt=$now WHERE Provider=$provider;", cancellationToken,
                ("$provider", provider.ToString()), ("$now", Format(now)));
        }
    }

    public async Task<IReadOnlyList<AiModelOption>> GetAiModelsAsync(AiProviderKind provider, bool allowExpired = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = allowExpired ? "SELECT ModelsJson FROM AiModelCache WHERE Provider = $provider;" : "SELECT ModelsJson FROM AiModelCache WHERE Provider = $provider AND ExpiresAt > $now;";
        command.Parameters.AddWithValue("$provider", provider.ToString()); command.Parameters.AddWithValue("$now", Format(_time.GetUtcNow()));
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return json is null ? [] : JsonSerializer.Deserialize<List<AiModelOption>>(json, ContentTypeRegistry.JsonOptions) ?? [];
    }

    public async Task<AiContextAssetRecord> SaveAiContextAssetAsync(string fileName, string mediaType, byte[] content, string extractedText, Guid actorId, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid(); var now = _time.GetUtcNow(); var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(content));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO AiContextAssets (Id, FileNameEncrypted, MediaType, Size, Sha256, ContentEncrypted, ExtractedTextEncrypted, CreatedBy, CreatedAt)
            VALUES ($id, $name, $type, $size, $sha, $content, $text, $actor, $now);
            """, cancellationToken, ("$id", id.ToString()), ("$name", _encryption.Encrypt(fileName)), ("$type", mediaType), ("$size", content.LongLength),
            ("$sha", sha), ("$content", _encryption.EncryptBytes(content)), ("$text", _encryption.Encrypt(extractedText)), ("$actor", actorId.ToString()), ("$now", Format(now)));
        await InsertAuditAsync(connection, actorId, "ai_context.uploaded", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id, fileName, mediaType, content.LongLength, sha, now);
    }

    public async Task<long> GetAiContextLibrarySizeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COALESCE(SUM(Size), 0) FROM AiContextAssets;";
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public async Task<IReadOnlyList<AiContextAssetRecord>> ListAiContextAssetsAsync(CancellationToken cancellationToken = default)
    {
        var values = new List<AiContextAssetRecord>(); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "SELECT Id, FileNameEncrypted, MediaType, Size, Sha256, CreatedAt FROM AiContextAssets ORDER BY CreatedAt DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(new(Guid.Parse(reader.GetString(0)), _encryption.Decrypt(reader.GetString(1)), reader.GetString(2), reader.GetInt64(3), reader.GetString(4), Parse(reader.GetString(5))));
        return values;
    }

    public async Task<(string FileName, string Text)?> GetAiContextTextAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT FileNameEncrypted, ExtractedTextEncrypted FROM AiContextAssets WHERE Id = $id;"; command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? (_encryption.Decrypt(reader.GetString(0)), _encryption.Decrypt(reader.GetString(1))) : null;
    }

    public async Task DeleteAiContextAssetAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM AiContextAssets WHERE Id = $id;", cancellationToken, ("$id", id.ToString()));
        await InsertAuditAsync(connection, actorId, "ai_context.deleted", id.ToString(), cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AiConversationRecord> CreateAiConversationAsync(string documentKey, AiProviderKind provider, string modelId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid(); var now = _time.GetUtcNow(); await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "INSERT INTO AiConversations (Id, DocumentKey, Provider, ModelId, CreatedBy, CreatedAt, LastActiveAt) VALUES ($id,$key,$provider,$model,$actor,$now,$now);", cancellationToken,
            ("$id", id.ToString()), ("$key", documentKey), ("$provider", provider.ToString()), ("$model", modelId), ("$actor", actorId.ToString()), ("$now", Format(now)));
        return new(id, documentKey, provider, modelId, now, now);
    }

    public async Task<AiConversationRecord?> GetAiConversationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,DocumentKey,Provider,ModelId,CreatedAt,LastActiveAt FROM AiConversations WHERE Id=$id;"; command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(Guid.Parse(reader.GetString(0)), reader.GetString(1), Enum.Parse<AiProviderKind>(reader.GetString(2)), reader.GetString(3), Parse(reader.GetString(4)), Parse(reader.GetString(5))) : null;
    }

    public async Task<IReadOnlyList<AiConversationRecord>> ListAiConversationsAsync(CancellationToken cancellationToken = default)
    {
        var values = new List<AiConversationRecord>(); await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,DocumentKey,Provider,ModelId,CreatedAt,LastActiveAt FROM AiConversations ORDER BY LastActiveAt DESC LIMIT 30;"; await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), Enum.Parse<AiProviderKind>(reader.GetString(2)), reader.GetString(3), Parse(reader.GetString(4)), Parse(reader.GetString(5)))); return values;
    }

    public async Task<IReadOnlyList<AiMessageRecord>> ListAiMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var values = new List<AiMessageRecord>(); await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,ConversationId,Role,BodyEncrypted,CreatedAt FROM AiMessages WHERE ConversationId=$id ORDER BY CreatedAt;"; command.Parameters.AddWithValue("$id", conversationId.ToString()); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) values.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.GetString(2), _encryption.Decrypt(reader.GetString(3)), Parse(reader.GetString(4)))); return values;
    }

    public async Task DeleteAiConversationAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM AiConversations WHERE Id=$id;", cancellationToken, ("$id", id.ToString())); await InsertAuditAsync(connection, actorId, "ai_conversation.deleted", id.ToString(), cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task AddAiMessageAsync(Guid conversationId, string role, string body, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow(); await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "INSERT INTO AiMessages (Id, ConversationId, Role, BodyEncrypted, CreatedAt) VALUES ($id,$conversation,$role,$body,$now);", cancellationToken,
            ("$id", Guid.NewGuid().ToString()), ("$conversation", conversationId.ToString()), ("$role", role), ("$body", _encryption.Encrypt(body)), ("$now", Format(now)));
        await ExecuteAsync(connection, "UPDATE AiConversations SET LastActiveAt = $now WHERE Id = $id;", cancellationToken, ("$now", Format(now)), ("$id", conversationId.ToString()));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AiProposalRecord> SaveAiProposalAsync(Guid conversationId, string documentKey, long baseRevision, ContentProposalDocument proposal, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid(); var now = _time.GetUtcNow(); await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "INSERT INTO AiProposals (Id, ConversationId, DocumentKey, BaseRevision, SummaryEncrypted, ProposedContentEncrypted, Status, CreatedAt) VALUES ($id,$conversation,$key,$revision,$summary,$content,'Proposed',$now);", cancellationToken,
            ("$id", id.ToString()), ("$conversation", conversationId.ToString()), ("$key", documentKey), ("$revision", baseRevision),
            ("$summary", _encryption.Encrypt(proposal.Summary)), ("$content", _encryption.Encrypt(proposal.ContentJson)), ("$now", Format(now)));
        return new(id, conversationId, documentKey, baseRevision, proposal.Summary, proposal.ContentJson, AiProposalStatus.Proposed, now);
    }

    public async Task<AiProposalRecord?> GetAiProposalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM AiProposals WHERE Id = $id;"; command.Parameters.AddWithValue("$id", id.ToString()); await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new(Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))), Guid.Parse(reader.GetString(reader.GetOrdinal("ConversationId"))),
            reader.GetString(reader.GetOrdinal("DocumentKey")), reader.GetInt64(reader.GetOrdinal("BaseRevision")), _encryption.Decrypt(reader.GetString(reader.GetOrdinal("SummaryEncrypted"))),
            _encryption.Decrypt(reader.GetString(reader.GetOrdinal("ProposedContentEncrypted"))), Enum.Parse<AiProposalStatus>(reader.GetString(reader.GetOrdinal("Status"))), Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))) : null;
    }

    public async Task MarkAiProposalAppliedAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE AiProposals SET Status = 'Applied', AppliedAt = $now WHERE Id = $id AND Status = 'Proposed';", cancellationToken, ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()));
        await InsertAuditAsync(connection, actorId, "ai_proposal.applied", id.ToString(), cancellationToken); await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid?> ReserveAiBudgetAsync(AiProviderKind provider, decimal estimatedUsd, CancellationToken cancellationToken = default)
    {
        if (estimatedUsd <= 0) estimatedUsd = 0.000001m;
        var now = _time.GetUtcNow(); var period = now.ToString("yyyy-MM", CultureInfo.InvariantCulture); var id = Guid.NewGuid();
        await using var connection = await OpenAsync(cancellationToken); await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        await using var budgetCommand = connection.CreateCommand(); budgetCommand.CommandText = "SELECT MonthlyBudgetUsd FROM AiProviderConfigurations WHERE Provider = $provider;"; budgetCommand.Parameters.AddWithValue("$provider", provider.ToString());
        var budget = Convert.ToDecimal(await budgetCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await using var usageCommand = connection.CreateCommand(); usageCommand.CommandText = "SELECT COALESCE(SUM(CASE WHEN Status='Reserved' THEN EstimatedUsd ELSE ActualUsd END),0) FROM AiUsageLedger WHERE Provider=$provider AND Period=$period AND Status IN ('Reserved','Completed','Failed');";
        usageCommand.Parameters.AddWithValue("$provider", provider.ToString()); usageCommand.Parameters.AddWithValue("$period", period);
        var used = Convert.ToDecimal(await usageCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await using var globalBudgetCommand = connection.CreateCommand();
        globalBudgetCommand.CommandText = "SELECT MonthlyBudgetUsd FROM AiGlobalSettings WHERE Id = 1;";
        var globalBudget = Convert.ToDecimal(await globalBudgetCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await using var globalUsageCommand = connection.CreateCommand();
        globalUsageCommand.CommandText = "SELECT COALESCE(SUM(CASE WHEN Status='Reserved' THEN EstimatedUsd ELSE ActualUsd END),0) FROM AiUsageLedger WHERE Period=$period AND Status IN ('Reserved','Completed','Failed');";
        globalUsageCommand.Parameters.AddWithValue("$period", period);
        var globalUsed = Convert.ToDecimal(await globalUsageCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        if (used + estimatedUsd > budget || globalUsed + estimatedUsd > globalBudget) { await transaction.RollbackAsync(cancellationToken); return null; }
        await ExecuteAsync(connection, "INSERT INTO AiUsageLedger (Id,Provider,Period,EstimatedUsd,ActualUsd,Status,CreatedAt) VALUES ($id,$provider,$period,$estimate,0,'Reserved',$now);", cancellationToken,
            ("$id", id.ToString()), ("$provider", provider.ToString()), ("$period", period), ("$estimate", estimatedUsd), ("$now", Format(now)));
        await transaction.CommitAsync(cancellationToken); return id;
    }

    public async Task CompleteAiUsageAsync(Guid id, decimal actualUsd, long inputTokens, long outputTokens, string status, CancellationToken cancellationToken = default,
        bool? zeroDataRetentionObserved = null, string? providerRequestId = null)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE AiUsageLedger SET ActualUsd=$actual,InputTokens=$input,OutputTokens=$output,Status=$status,CompletedAt=$now,ZeroDataRetentionObserved=$zdr,ProviderRequestId=$requestId WHERE Id=$id;", cancellationToken,
            ("$actual", Math.Max(0, actualUsd)), ("$input", inputTokens), ("$output", outputTokens), ("$status", status), ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()),
            ("$zdr", zeroDataRetentionObserved is null ? null : zeroDataRetentionObserved.Value ? 1 : 0), ("$requestId", providerRequestId));
    }

    public async Task<AiBudgetStatus> GetAiBudgetStatusAsync(AiProviderKind provider, CancellationToken cancellationToken = default)
    {
        var period = _time.GetUtcNow().ToString("yyyy-MM", CultureInfo.InvariantCulture); await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = """
            SELECT c.MonthlyBudgetUsd,
              COALESCE(SUM(CASE WHEN u.Status IN ('Completed','Failed') THEN u.ActualUsd ELSE 0 END),0),
              COALESCE(SUM(CASE WHEN u.Status='Reserved' THEN u.EstimatedUsd ELSE 0 END),0)
            FROM AiProviderConfigurations c LEFT JOIN AiUsageLedger u ON u.Provider=c.Provider AND u.Period=$period
            WHERE c.Provider=$provider GROUP BY c.MonthlyBudgetUsd;
            """; command.Parameters.AddWithValue("$period", period); command.Parameters.AddWithValue("$provider", provider.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); if (!await reader.ReadAsync(cancellationToken)) return new(0, 0, 0, 0, period);
        var budget = reader.GetDecimal(0); var used = reader.GetDecimal(1); var reserved = reader.GetDecimal(2); return new(budget, used, reserved, Math.Max(0, budget - used - reserved), period);
    }

    public async Task<AiBudgetStatus> GetAiGlobalBudgetStatusAsync(CancellationToken cancellationToken = default)
    {
        var period = _time.GetUtcNow().ToString("yyyy-MM", CultureInfo.InvariantCulture);
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT g.MonthlyBudgetUsd,
              COALESCE(SUM(CASE WHEN u.Status IN ('Completed','Failed') THEN u.ActualUsd ELSE 0 END),0),
              COALESCE(SUM(CASE WHEN u.Status='Reserved' THEN u.EstimatedUsd ELSE 0 END),0)
            FROM AiGlobalSettings g LEFT JOIN AiUsageLedger u ON u.Period=$period
            WHERE g.Id=1 GROUP BY g.MonthlyBudgetUsd;
            """;
        command.Parameters.AddWithValue("$period", period);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return new(0, 0, 0, 0, period);
        var budget = reader.GetDecimal(0); var used = reader.GetDecimal(1); var reserved = reader.GetDecimal(2);
        return new(budget, used, reserved, Math.Max(0, budget - used - reserved), period);
    }

    public async Task SaveAiGlobalBudgetAsync(decimal monthlyBudgetUsd, Guid actorId, CancellationToken cancellationToken = default)
    {
        if (monthlyBudgetUsd is <= 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(monthlyBudgetUsd));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE AiGlobalSettings SET MonthlyBudgetUsd=$budget, UpdatedAt=$now, UpdatedBy=$actor WHERE Id=1;", cancellationToken,
            ("$budget", monthlyBudgetUsd), ("$now", Format(_time.GetUtcNow())), ("$actor", actorId.ToString()));
        await InsertAuditAsync(connection, actorId, "ai_budget.global_updated", monthlyBudgetUsd.ToString(CultureInfo.InvariantCulture), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static AiProviderConfigurationRecord ReadAiProvider(SqliteDataReader reader) => new(
        Enum.Parse<AiProviderKind>(reader.GetString(reader.GetOrdinal("Provider"))), reader.GetString(reader.GetOrdinal("KeyFingerprint")), reader.GetString(reader.GetOrdinal("KeyLastFour")),
        reader.IsDBNull(reader.GetOrdinal("SelectedModel")) ? null : reader.GetString(reader.GetOrdinal("SelectedModel")), reader.GetDecimal(reader.GetOrdinal("MonthlyBudgetUsd")),
        reader.GetInt32(reader.GetOrdinal("MaximumOutputTokens")), Enum.Parse<AiProviderStatus>(reader.GetString(reader.GetOrdinal("Status"))),
        reader.IsDBNull(reader.GetOrdinal("LastTestedAt")) ? null : Parse(reader.GetString(reader.GetOrdinal("LastTestedAt"))),
        reader.IsDBNull(reader.GetOrdinal("LastErrorCode")) ? null : reader.GetString(reader.GetOrdinal("LastErrorCode")),
        reader.IsDBNull(reader.GetOrdinal("ZeroDataRetentionObserved")) ? null : reader.GetInt32(reader.GetOrdinal("ZeroDataRetentionObserved")) == 1,
        Parse(reader.GetString(reader.GetOrdinal("UpdatedAt"))));

    private const string AiSchema = """
        INSERT OR IGNORE INTO SchemaVersions (Version, AppliedAt) VALUES (3, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
        CREATE TABLE IF NOT EXISTS AiGlobalSettings (Id INTEGER PRIMARY KEY CHECK (Id = 1), MonthlyBudgetUsd NUMERIC NOT NULL, UpdatedAt TEXT NOT NULL, UpdatedBy TEXT NULL);
        INSERT OR IGNORE INTO AiGlobalSettings (Id, MonthlyBudgetUsd, UpdatedAt, UpdatedBy) VALUES (1, 20, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'), NULL);
        CREATE TABLE IF NOT EXISTS AiProviderConfigurations (Provider TEXT PRIMARY KEY, ApiKeyEncrypted TEXT NOT NULL, KeyFingerprint TEXT NOT NULL, KeyLastFour TEXT NOT NULL, SelectedModel TEXT NULL, MonthlyBudgetUsd NUMERIC NOT NULL, MaximumOutputTokens INTEGER NOT NULL, Status TEXT NOT NULL, LastTestedAt TEXT NULL, LastErrorCode TEXT NULL, ZeroDataRetentionObserved INTEGER NULL, UpdatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS AiModelCache (Provider TEXT PRIMARY KEY, ModelsJson TEXT NOT NULL, FetchedAt TEXT NOT NULL, ExpiresAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS AiContextAssets (Id TEXT PRIMARY KEY, FileNameEncrypted TEXT NOT NULL, MediaType TEXT NOT NULL, Size INTEGER NOT NULL, Sha256 TEXT NOT NULL, ContentEncrypted BLOB NOT NULL, ExtractedTextEncrypted TEXT NOT NULL, CreatedBy TEXT NOT NULL, CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS AiConversations (Id TEXT PRIMARY KEY, DocumentKey TEXT NOT NULL, Provider TEXT NOT NULL, ModelId TEXT NOT NULL, CreatedBy TEXT NOT NULL, CreatedAt TEXT NOT NULL, LastActiveAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS AiMessages (Id TEXT PRIMARY KEY, ConversationId TEXT NOT NULL REFERENCES AiConversations(Id) ON DELETE CASCADE, Role TEXT NOT NULL, BodyEncrypted TEXT NOT NULL, CreatedAt TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS AiProposals (Id TEXT PRIMARY KEY, ConversationId TEXT NOT NULL REFERENCES AiConversations(Id) ON DELETE CASCADE, DocumentKey TEXT NOT NULL, BaseRevision INTEGER NOT NULL, SummaryEncrypted TEXT NOT NULL, ProposedContentEncrypted TEXT NOT NULL, Status TEXT NOT NULL, CreatedAt TEXT NOT NULL, AppliedAt TEXT NULL);
        CREATE TABLE IF NOT EXISTS AiUsageLedger (Id TEXT PRIMARY KEY, Provider TEXT NOT NULL, Period TEXT NOT NULL, EstimatedUsd NUMERIC NOT NULL, ActualUsd NUMERIC NOT NULL, InputTokens INTEGER NOT NULL DEFAULT 0, OutputTokens INTEGER NOT NULL DEFAULT 0, Status TEXT NOT NULL, ZeroDataRetentionObserved INTEGER NULL, ProviderRequestId TEXT NULL, CreatedAt TEXT NOT NULL, CompletedAt TEXT NULL);
        CREATE INDEX IF NOT EXISTS IX_AiUsageLedger_Provider_Period ON AiUsageLedger(Provider, Period);
        """;
}

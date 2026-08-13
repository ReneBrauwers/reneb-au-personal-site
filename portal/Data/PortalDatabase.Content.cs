using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Data;

public sealed partial class PortalDatabase
{
    public async Task<ContentSnapshot<T>> GetContentAsync<T>(string key, bool draft, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await GetContentAsync<T>(connection, key, draft, cancellationToken);
    }

    public async Task<string> GetContentJsonAsync(string key, bool draft, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = draft
            ? """
                SELECT r.ContentEncrypted FROM ContentDocuments d
                JOIN ContentRevisions r ON r.Id = d.DraftRevisionId WHERE d.Key = $key;
                """
            : """
                SELECT r.ContentEncrypted FROM ContentDocuments d
                JOIN ContentRevisions r ON r.Id = d.PublishedRevisionId WHERE d.Key = $key;
                """;
        command.Parameters.AddWithValue("$key", key);
        var encrypted = (string?)await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Content document '{key}' has not been initialized.");
        return _encryption.Decrypt(encrypted);
    }

    public async Task<IReadOnlyList<ContentDocumentRecord>> ListContentDocumentsAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<ContentDocumentRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.Key, d.ContentType, dr.Revision, pr.Revision, d.UpdatedAt, d.PublishedAt
            FROM ContentDocuments d
            JOIN ContentRevisions dr ON dr.Id = d.DraftRevisionId
            JOIN ContentRevisions pr ON pr.Id = d.PublishedRevisionId
            ORDER BY d.Key;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ContentDocumentRecord(
                reader.GetString(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3),
                Parse(reader.GetString(4)), Parse(reader.GetString(5))));
        }
        return records;
    }

    public async Task<IReadOnlyList<ContentRevisionRecord>> ListContentRevisionsAsync(string key, CancellationToken cancellationToken = default)
    {
        var records = new List<ContentRevisionRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.Id, r.DocumentKey, r.Revision, r.SchemaVersion, r.CreatedBy, r.CreatedAt,
                   CASE WHEN r.Id = d.DraftRevisionId THEN 1 ELSE 0 END,
                   CASE WHEN r.Id = d.PublishedRevisionId THEN 1 ELSE 0 END
            FROM ContentRevisions r JOIN ContentDocuments d ON d.Key = r.DocumentKey
            WHERE r.DocumentKey = $key ORDER BY r.Revision DESC LIMIT 20;
            """;
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(new ContentRevisionRecord(
                Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetInt64(2), reader.GetInt32(3),
                reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)), Parse(reader.GetString(5)),
                reader.GetInt32(6) == 1, reader.GetInt32(7) == 1));
        }
        return records;
    }

    public Task<ContentSnapshot<T>> SaveContentDraftAsync<T>(string key, T content, long expectedRevision, Guid actorId, CancellationToken cancellationToken = default)
        where T : class
        => WriteContentAsync(key, content, expectedRevision, actorId, publish: false, null, cancellationToken);

    public Task<ContentSnapshot<T>> PublishContentAsync<T>(string key, T content, long expectedRevision, Guid actorId, CancellationToken cancellationToken = default)
        where T : class
        => WriteContentAsync(key, content, expectedRevision, actorId, publish: true, null, cancellationToken);

    public async Task<ContentSnapshot<object>> SaveContentJsonDraftAsync(string key, string json, long expectedRevision, Guid actorId, CancellationToken cancellationToken = default)
    {
        var content = ContentTypeRegistry.DeserializeAndValidate(key, json);
        return await WriteUntypedContentAsync(key, content, expectedRevision, actorId, publish: false, null, cancellationToken);
    }

    public async Task<ContentSnapshot<object>> PublishContentJsonAsync(string key, string json, long expectedRevision, Guid actorId, CancellationToken cancellationToken = default)
    {
        var content = ContentTypeRegistry.DeserializeAndValidate(key, json);
        return await WriteUntypedContentAsync(key, content, expectedRevision, actorId, publish: true, null, cancellationToken);
    }

    public async Task RollbackContentAsync(string key, Guid revisionId, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var selected = connection.CreateCommand();
        selected.CommandText = "SELECT ContentEncrypted FROM ContentRevisions WHERE Id = $id AND DocumentKey = $key;";
        selected.Parameters.AddWithValue("$id", revisionId.ToString());
        selected.Parameters.AddWithValue("$key", key);
        var encrypted = (string?)await selected.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException("The selected content revision does not exist.");
        var json = _encryption.Decrypt(encrypted);
        var content = ContentTypeRegistry.DeserializeAndValidate(key, json);
        var current = await GetDocumentRevisionAsync(connection, key, cancellationToken);
        var result = await InsertContentRevisionAsync(connection, key, content, current + 1, actorId, cancellationToken);
        await ExecuteAsync(connection, """
            UPDATE ContentDocuments SET DraftRevisionId = $id, PublishedRevisionId = $id, UpdatedAt = $now, PublishedAt = $now WHERE Key = $key;
            """, cancellationToken, ("$id", result.Id.ToString()), ("$now", Format(_time.GetUtcNow())), ("$key", key));
        await DualWriteLegacyAsync(connection, key, content, publish: true, cancellationToken);
        await InsertAuditAsync(connection, actorId, "content.rolled_back", $"{key}:{revisionId}", cancellationToken);
        await TrimContentRevisionsAsync(connection, key, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<ContentSnapshot<T>> WriteContentAsync<T>(string key, T content, long expectedRevision, Guid actorId, bool publish,
        Func<SqliteConnection, T, bool, CancellationToken, Task>? extraWrite, CancellationToken cancellationToken) where T : class
    {
        var result = await WriteUntypedContentAsync(key, content, expectedRevision, actorId, publish,
            extraWrite is null ? null : (connection, value, state, token) => extraWrite(connection, (T)value, state, token), cancellationToken);
        return new ContentSnapshot<T>((T)result.Content, result.Revision, result.UpdatedAt);
    }

    private async Task<ContentSnapshot<object>> WriteUntypedContentAsync(string key, object content, long expectedRevision, Guid actorId, bool publish,
        Func<SqliteConnection, object, bool, CancellationToken, Task>? extraWrite, CancellationToken cancellationToken)
    {
        if (content.GetType() != ContentTypeRegistry.GetType(key)) throw new ValidationException("Content type does not match the selected document.");
        NormalizeContent(content);
        ContentTypeRegistry.ValidateGraph(content);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await GetDocumentRevisionAsync(connection, key, cancellationToken);
        if (current != expectedRevision) throw new ContentConcurrencyException(current);
        var now = _time.GetUtcNow();
        var revision = await InsertContentRevisionAsync(connection, key, content, current + 1, actorId, cancellationToken);
        await ExecuteAsync(connection, publish
            ? "UPDATE ContentDocuments SET DraftRevisionId = $id, PublishedRevisionId = $id, UpdatedAt = $now, PublishedAt = $now WHERE Key = $key;"
            : "UPDATE ContentDocuments SET DraftRevisionId = $id, UpdatedAt = $now WHERE Key = $key;",
            cancellationToken, ("$id", revision.Id.ToString()), ("$now", Format(now)), ("$key", key));
        await DualWriteLegacyAsync(connection, key, content, publish, cancellationToken);
        if (extraWrite is not null) await extraWrite(connection, content, publish, cancellationToken);
        await InsertAuditAsync(connection, actorId, publish ? "content.published" : "content.draft_saved", key, cancellationToken);
        await TrimContentRevisionsAsync(connection, key, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ContentSnapshot<object>(content, revision.Revision, now);
    }

    private async Task<ContentSnapshot<T>> GetContentAsync<T>(SqliteConnection connection, string key, bool draft, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = draft
            ? """
                SELECT r.ContentEncrypted, r.Revision, d.UpdatedAt FROM ContentDocuments d
                JOIN ContentRevisions r ON r.Id = d.DraftRevisionId WHERE d.Key = $key;
                """
            : """
                SELECT r.ContentEncrypted, r.Revision, d.PublishedAt FROM ContentDocuments d
                JOIN ContentRevisions r ON r.Id = d.PublishedRevisionId WHERE d.Key = $key;
                """;
        command.Parameters.AddWithValue("$key", key);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException($"Content document '{key}' has not been initialized.");
        var content = JsonSerializer.Deserialize<T>(_encryption.Decrypt(reader.GetString(0)), ContentTypeRegistry.JsonOptions)
            ?? throw new InvalidOperationException($"Content document '{key}' is invalid.");
        NormalizeContent(content!);
        return new ContentSnapshot<T>(content, reader.GetInt64(1), Parse(reader.GetString(2)));
    }

    private async Task InitializeContentDocumentsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM ContentDocuments;";
        if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0) return;

        await using var candidate = connection.CreateCommand();
        candidate.CommandText = "SELECT DraftPublicJson, PublishedPublicJson, PrivateEncrypted FROM CandidateProfiles WHERE Id = 1;";
        await using var reader = await candidate.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new InvalidOperationException("Legacy profile was not initialized.");
        var draftProfile = JsonSerializer.Deserialize<PublicCandidateProfile>(reader.GetString(0), JsonOptions) ?? PublicProfileDefaults.Create();
        var publishedProfile = JsonSerializer.Deserialize<PublicCandidateProfile>(reader.GetString(1), JsonOptions) ?? PublicProfileDefaults.Create();
        var privateProfile = JsonSerializer.Deserialize<PrivateCandidateProfile>(_encryption.Decrypt(reader.GetString(2)), JsonOptions) ?? new();
        await reader.DisposeAsync();
        NormalizeContent(draftProfile); NormalizeContent(publishedProfile); NormalizeContent(privateProfile);

        await InsertInitialDocumentAsync(connection, ContentDocumentKeys.Home, ContentDefaults.Home(), ContentDefaults.Home(), cancellationToken);
        await InsertInitialDocumentAsync(connection, ContentDocumentKeys.SiteSettings, ContentDefaults.SiteSettings(), ContentDefaults.SiteSettings(), cancellationToken);
        await InsertInitialDocumentAsync(connection, ContentDocumentKeys.RecruiterProfile, draftProfile, publishedProfile, cancellationToken);
        await InsertInitialDocumentAsync(connection, ContentDocumentKeys.OpportunityProfile, privateProfile, privateProfile, cancellationToken);
        await InsertInitialDocumentAsync(connection, ContentDocumentKeys.Privacy, ContentDefaults.Privacy(), ContentDefaults.Privacy(), cancellationToken);
        await InsertInitialDocumentAsync(connection, ContentDocumentKeys.Discovery, ContentDefaults.Discovery(), ContentDefaults.Discovery(), cancellationToken);
    }

    private async Task InsertInitialDocumentAsync(SqliteConnection connection, string key, object draft, object published, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        await ExecuteAsync(connection, """
            INSERT INTO ContentDocuments (Key, ContentType, DraftRevisionId, PublishedRevisionId, UpdatedAt, PublishedAt)
            VALUES ($key, $type, NULL, NULL, $now, $now);
            """, cancellationToken, ("$key", key), ("$type", ContentTypeRegistry.TypeName(key)), ("$now", Format(now)));
        var publishedRevision = await InsertContentRevisionAsync(connection, key, published, 1, null, cancellationToken);
        var draftJson = JsonSerializer.Serialize(draft, draft.GetType(), ContentTypeRegistry.JsonOptions);
        var publishedJson = JsonSerializer.Serialize(published, published.GetType(), ContentTypeRegistry.JsonOptions);
        var draftRevision = string.Equals(draftJson, publishedJson, StringComparison.Ordinal)
            ? publishedRevision
            : await InsertContentRevisionAsync(connection, key, draft, 2, null, cancellationToken);
        await ExecuteAsync(connection, """
            UPDATE ContentDocuments SET DraftRevisionId = $draft, PublishedRevisionId = $published WHERE Key = $key;
            """, cancellationToken, ("$key", key), ("$draft", draftRevision.Id.ToString()), ("$published", publishedRevision.Id.ToString()));
    }

    private async Task<(Guid Id, long Revision)> InsertContentRevisionAsync(SqliteConnection connection, string key, object content, long revision,
        Guid? actorId, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(content, content.GetType(), ContentTypeRegistry.JsonOptions);
        await ExecuteAsync(connection, """
            INSERT INTO ContentRevisions (Id, DocumentKey, Revision, SchemaVersion, ContentEncrypted, CreatedBy, CreatedAt)
            VALUES ($id, $key, $revision, 1, $content, $actor, $now);
            """, cancellationToken, ("$id", id.ToString()), ("$key", key), ("$revision", revision),
            ("$content", _encryption.Encrypt(json)), ("$actor", actorId?.ToString()), ("$now", Format(_time.GetUtcNow())));
        return (id, revision);
    }

    private async Task<long> GetDocumentRevisionAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT r.Revision FROM ContentDocuments d JOIN ContentRevisions r ON r.Id = d.DraftRevisionId WHERE d.Key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private async Task DualWriteLegacyAsync(SqliteConnection connection, string key, object content, bool publish, CancellationToken cancellationToken)
    {
        if (key == ContentDocumentKeys.RecruiterProfile)
        {
            var json = JsonSerializer.Serialize((PublicCandidateProfile)content, JsonOptions);
            await ExecuteAsync(connection, publish
                ? "UPDATE CandidateProfiles SET DraftPublicJson = $json, PublishedPublicJson = $json, UpdatedAt = $now, PublishedAt = $now WHERE Id = 1;"
                : "UPDATE CandidateProfiles SET DraftPublicJson = $json, UpdatedAt = $now WHERE Id = 1;",
                cancellationToken, ("$json", json), ("$now", Format(_time.GetUtcNow())));
        }
        else if (key == ContentDocumentKeys.OpportunityProfile && publish)
        {
            var encrypted = _encryption.Encrypt(JsonSerializer.Serialize((PrivateCandidateProfile)content, JsonOptions));
            await ExecuteAsync(connection, "UPDATE CandidateProfiles SET PrivateEncrypted = $content, UpdatedAt = $now WHERE Id = 1;", cancellationToken,
                ("$content", encrypted), ("$now", Format(_time.GetUtcNow())));
        }
    }

    private async Task TrimContentRevisionsAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
        => await ExecuteAsync(connection, """
            DELETE FROM ContentRevisions
            WHERE DocumentKey = $key
              AND Id NOT IN (SELECT DraftRevisionId FROM ContentDocuments WHERE Key = $key)
              AND Id NOT IN (SELECT PublishedRevisionId FROM ContentDocuments WHERE Key = $key)
              AND Id NOT IN (SELECT Id FROM ContentRevisions WHERE DocumentKey = $key ORDER BY Revision DESC LIMIT 20);
            """, cancellationToken, ("$key", key));

    private static void NormalizeContent(object content)
    {
        if (content is PublicCandidateProfile profile)
        {
            if (string.IsNullOrWhiteSpace(RichTextDelta.ToPlainText(profile.SummaryRichText)))
                profile.SummaryRichText = RichTextContent.FromParagraphs(profile.Summary);
            profile.Summary = RichTextDelta.ToPlainText(profile.SummaryRichText);
        }
        else if (content is PrivateCandidateProfile opportunity)
        {
            if (string.IsNullOrWhiteSpace(RichTextDelta.ToPlainText(opportunity.DetailedInterestsRichText)) && !string.IsNullOrWhiteSpace(opportunity.DetailedInterests))
                opportunity.DetailedInterestsRichText = RichTextContent.FromParagraphs(opportunity.DetailedInterests);
            if (string.IsNullOrWhiteSpace(RichTextDelta.ToPlainText(opportunity.AdditionalGuidanceRichText)) && !string.IsNullOrWhiteSpace(opportunity.AdditionalGuidance))
                opportunity.AdditionalGuidanceRichText = RichTextContent.FromParagraphs(opportunity.AdditionalGuidance);
            opportunity.DetailedInterests = RichTextDelta.ToPlainText(opportunity.DetailedInterestsRichText);
            opportunity.AdditionalGuidance = RichTextDelta.ToPlainText(opportunity.AdditionalGuidanceRichText);
        }
    }

    private const string ExtendedSchema = """
        INSERT OR IGNORE INTO SchemaVersions (Version, AppliedAt) VALUES (2, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));

        CREATE TABLE IF NOT EXISTS ContentDocuments (
            Key TEXT PRIMARY KEY,
            ContentType TEXT NOT NULL,
            DraftRevisionId TEXT NULL,
            PublishedRevisionId TEXT NULL,
            UpdatedAt TEXT NOT NULL,
            PublishedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS ContentRevisions (
            Id TEXT PRIMARY KEY,
            DocumentKey TEXT NOT NULL REFERENCES ContentDocuments(Key) ON DELETE CASCADE DEFERRABLE INITIALLY DEFERRED,
            Revision INTEGER NOT NULL,
            SchemaVersion INTEGER NOT NULL,
            ContentEncrypted TEXT NOT NULL,
            CreatedBy TEXT NULL,
            CreatedAt TEXT NOT NULL,
            UNIQUE(DocumentKey, Revision)
        );
        CREATE INDEX IF NOT EXISTS IX_ContentRevisions_DocumentKey_Revision ON ContentRevisions(DocumentKey, Revision DESC);
        """;
}

public sealed class ContentConcurrencyException(long currentRevision) : Exception("The draft changed after this editor was opened.")
{
    public long CurrentRevision { get; } = currentRevision;
}

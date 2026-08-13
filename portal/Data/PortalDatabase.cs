using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Models;
using ReneB.Portal.Security;

namespace ReneB.Portal.Data;

public sealed partial class PortalDatabase
{
    private readonly string _connectionString;
    private readonly string _backupDirectory;
    private readonly FieldEncryptionService _encryption;
    private readonly AiCredentialEncryptionService _aiCredentials;
    private readonly TimeProvider _time;
    private readonly AiOptions _aiOptions;
    private readonly SemaphoreSlim _migrationLock = new(1, 1);

    public PortalDatabase(
        IOptions<PortalOptions> portalOptions,
        IOptions<AiOptions> aiOptions,
        FieldEncryptionService encryption,
        AiCredentialEncryptionService aiCredentials,
        TimeProvider time)
    {
        Directory.CreateDirectory(portalOptions.Value.DataDirectory);
        _backupDirectory = portalOptions.Value.BackupDirectory;
        _aiOptions = aiOptions.Value;
        _encryption = encryption;
        _aiCredentials = aiCredentials;
        _time = time;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(portalOptions.Value.DataDirectory, "recruiter-portal.sqlite3"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5
        }.ToString();
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await _migrationLock.WaitAsync(cancellationToken);
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await ExecuteAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
            await ExecuteAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken);
            await ExecuteAsync(connection, Schema, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM CandidateProfiles WHERE Id = 1;";
            var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) > 0;
            if (!exists)
            {
                var profile = PublicProfileDefaults.Create();
                var json = JsonSerializer.Serialize(profile, JsonOptions);
                await using var insert = connection.CreateCommand();
                insert.CommandText = """
                    INSERT INTO CandidateProfiles (Id, DraftPublicJson, PublishedPublicJson, PrivateEncrypted, UpdatedAt, PublishedAt)
                    VALUES (1, $draft, $published, $private, $now, $now);
                    """;
                insert.Parameters.AddWithValue("$draft", json);
                insert.Parameters.AddWithValue("$published", json);
                insert.Parameters.AddWithValue("$private", _encryption.Encrypt(JsonSerializer.Serialize(new PrivateCandidateProfile(), JsonOptions)));
                insert.Parameters.AddWithValue("$now", Format(_time.GetUtcNow()));
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            await ExecuteAsync(connection, ExtendedSchema, cancellationToken);
            await ExecuteAsync(connection, AiSchema, cancellationToken);
            await InitializeContentDocumentsAsync(connection, cancellationToken);
        }
        finally
        {
            _migrationLock.Release();
        }
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM SchemaVersions WHERE Version = 1;";
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    public async Task<PublicCandidateProfile> GetPublicProfileAsync(bool draft, CancellationToken cancellationToken = default)
        => (await GetContentAsync<PublicCandidateProfile>(ContentDocumentKeys.RecruiterProfile, draft, cancellationToken)).Content;

    public async Task SavePublicDraftAsync(PublicCandidateProfile profile, Guid actorId, CancellationToken cancellationToken = default)
    {
        var current = await GetContentAsync<PublicCandidateProfile>(ContentDocumentKeys.RecruiterProfile, true, cancellationToken);
        await SaveContentDraftAsync(ContentDocumentKeys.RecruiterProfile, profile, current.Revision, actorId, cancellationToken);
    }

    public async Task PublishPublicProfileAsync(PublicCandidateProfile profile, Guid actorId, CancellationToken cancellationToken = default)
    {
        var current = await GetContentAsync<PublicCandidateProfile>(ContentDocumentKeys.RecruiterProfile, true, cancellationToken);
        await PublishContentAsync(ContentDocumentKeys.RecruiterProfile, profile, current.Revision, actorId, cancellationToken);
    }

    public async Task<PrivateCandidateProfile> GetPrivateProfileAsync(CancellationToken cancellationToken = default)
        => (await GetContentAsync<PrivateCandidateProfile>(ContentDocumentKeys.OpportunityProfile, false, cancellationToken)).Content;

    public async Task SavePrivateProfileAsync(PrivateCandidateProfile profile, Guid actorId, CancellationToken cancellationToken = default)
    {
        var current = await GetContentAsync<PrivateCandidateProfile>(ContentDocumentKeys.OpportunityProfile, true, cancellationToken);
        await PublishContentAsync(ContentDocumentKeys.OpportunityProfile, profile, current.Revision, actorId, cancellationToken);
    }

    public async Task<RecruiterRecord> UpsertPendingRecruiterAsync(RecruiterRegistration registration, DomainRisk domainRisk, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(registration.Email);
        var emailHash = _encryption.LookupHash(email);
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var existing = await FindRecruiterByHashAsync(connection, emailHash, cancellationToken);
        var id = existing?.Id ?? Guid.NewGuid();
        if (existing is null)
        {
            await ExecuteAsync(connection, """
                INSERT INTO Recruiters
                    (Id, EmailHash, EmailEncrypted, NameEncrypted, OrganisationEncrypted, TitleEncrypted,
                     ProfileUrlEncrypted, CountryEncrypted, PhoneEncrypted, PurposeEncrypted, DomainRisk,
                     Status, CreatedAt, LastActiveAt)
                VALUES
                    ($id, $emailHash, $email, $name, $organisation, $title, $profileUrl, $country, $phone,
                     $purpose, $domainRisk, $status, $now, $now);
                """, cancellationToken,
                ("$id", id.ToString()), ("$emailHash", emailHash), ("$email", _encryption.Encrypt(email)),
                ("$name", _encryption.Encrypt(registration.Name)), ("$organisation", _encryption.Encrypt(registration.Organisation)),
                ("$title", _encryption.Encrypt(registration.Title)), ("$profileUrl", _encryption.Encrypt(registration.ProfileUrl)),
                ("$country", _encryption.Encrypt(registration.Country)), ("$phone", _encryption.Encrypt(registration.Phone)),
                ("$purpose", _encryption.Encrypt(registration.Purpose)), ("$domainRisk", domainRisk.ToString()),
                ("$status", RecruiterStatus.PendingEmail.ToString()), ("$now", Format(now)));
        }
        else
        {
            // Registration is anonymous. Preserve the verified record and its status until mailbox ownership is proven.
            // A repeat request may issue a new challenge, but it cannot rewrite identity data or revoke access.
        }

        await InsertAuditAsync(connection, id, "recruiter.registration_requested", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (await GetRecruiterAsync(id, cancellationToken))!;
    }

    public async Task<RecruiterRecord?> EnsureAdminAccountAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEmail(email);
        var hash = _encryption.LookupHash(normalized);
        await using var connection = await OpenAsync(cancellationToken);
        var existing = await FindRecruiterByHashAsync(connection, hash, cancellationToken);
        if (existing is not null)
        {
            await ExecuteAsync(connection, """
                UPDATE Recruiters SET
                    IsAdmin = 1,
                    Status = CASE WHEN Status IN ('Suspended', 'Deleted') THEN 'PendingEmail' ELSE Status END,
                    DeletedAt = NULL
                WHERE Id = $id;
                """, cancellationToken, ("$id", existing.Id.ToString()));
            return await GetRecruiterAsync(existing.Id, cancellationToken);
        }

        var registration = new RecruiterRegistration("René Brauwers", normalized, "reneb.au", "Administrator", "https://reneb.au/", "Australia", null, "Portal administration");
        var account = await UpsertPendingRecruiterAsync(registration, DomainRisk.Business, cancellationToken);
        await ExecuteAsync(connection, "UPDATE Recruiters SET IsAdmin = 1 WHERE Id = $id;", cancellationToken, ("$id", account.Id.ToString()));
        return await GetRecruiterAsync(account.Id, cancellationToken);
    }

    public async Task ReconcileAdministratorsAsync(IEnumerable<string> adminEmails, CancellationToken cancellationToken = default)
    {
        var hashes = adminEmails
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => _encryption.LookupHash(NormalizeEmail(value)))
            .ToHashSet(StringComparer.Ordinal);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE Recruiters SET IsAdmin = 0;", cancellationToken);
        foreach (var hash in hashes)
        {
            await ExecuteAsync(connection, """
                UPDATE Recruiters SET
                    IsAdmin = 1,
                    Status = CASE WHEN Status IN ('Suspended', 'Deleted') THEN 'PendingEmail' ELSE Status END,
                    DeletedAt = NULL
                WHERE EmailHash = $hash;
                """, cancellationToken, ("$hash", hash));
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<RecruiterRecord?> FindRecruiterByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        return await FindRecruiterByHashAsync(connection, _encryption.LookupHash(NormalizeEmail(email)), cancellationToken);
    }

    public async Task<RecruiterRecord?> GetRecruiterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Recruiters WHERE Id = $id AND DeletedAt IS NULL;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecruiter(reader) : null;
    }

    public async Task<List<RecruiterRecord>> ListRecruitersAsync(CancellationToken cancellationToken = default)
    {
        var records = new List<RecruiterRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Recruiters WHERE DeletedAt IS NULL AND IsAdmin = 0 ORDER BY CreatedAt DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            records.Add(ReadRecruiter(reader));
        }
        return records;
    }

    public async Task<bool> CreateLoginChallengeAsync(Guid recruiterId, string tokenHash, string codeHash, DateTimeOffset expiresAt, int requestsPerMinute, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var throttle = connection.CreateCommand())
        {
            throttle.CommandText = "SELECT COUNT(*) FROM LoginChallenges WHERE RecruiterId = $recruiterId AND CreatedAt >= $cutoff;";
            throttle.Parameters.AddWithValue("$recruiterId", recruiterId.ToString());
            throttle.Parameters.AddWithValue("$cutoff", Format(_time.GetUtcNow().AddMinutes(-1)));
            var count = Convert.ToInt32(await throttle.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            if (count >= requestsPerMinute)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }
        }
        await ExecuteAsync(connection, "UPDATE LoginChallenges SET ConsumedAt = $now WHERE RecruiterId = $recruiterId AND ConsumedAt IS NULL;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$recruiterId", recruiterId.ToString()));
        await ExecuteAsync(connection, """
            INSERT INTO LoginChallenges (Id, RecruiterId, TokenHash, CodeHash, ExpiresAt, CreatedAt)
            VALUES ($id, $recruiterId, $tokenHash, $codeHash, $expiresAt, $now);
            """, cancellationToken,
            ("$id", Guid.NewGuid().ToString()), ("$recruiterId", recruiterId.ToString()), ("$tokenHash", tokenHash),
            ("$codeHash", codeHash), ("$expiresAt", Format(expiresAt)), ("$now", Format(_time.GetUtcNow())));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<RecruiterRecord?> ConsumeLoginChallengeAsync(string? tokenHash, string? email, string? codeHash, bool adminOnly = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = tokenHash is not null
            ? """
                UPDATE LoginChallenges SET ConsumedAt = $now
                WHERE Id = (
                    SELECT lc.Id FROM LoginChallenges lc
                    JOIN Recruiters r ON r.Id = lc.RecruiterId
                    WHERE lc.TokenHash = $value AND lc.ConsumedAt IS NULL AND lc.ExpiresAt > $now
                      AND ($adminOnly = 0 OR r.IsAdmin = 1) LIMIT 1
                ) AND ConsumedAt IS NULL
                RETURNING RecruiterId;
                """
            : """
                UPDATE LoginChallenges SET ConsumedAt = $now
                WHERE Id = (
                    SELECT lc.Id FROM LoginChallenges lc
                    JOIN Recruiters r ON r.Id = lc.RecruiterId
                    WHERE r.EmailHash = $emailHash AND lc.CodeHash = $value
                      AND lc.ConsumedAt IS NULL AND lc.ExpiresAt > $now
                      AND ($adminOnly = 0 OR r.IsAdmin = 1) LIMIT 1
                ) AND ConsumedAt IS NULL
                RETURNING RecruiterId;
                """;
        command.Parameters.AddWithValue("$value", tokenHash ?? codeHash ?? string.Empty);
        command.Parameters.AddWithValue("$now", Format(_time.GetUtcNow()));
        command.Parameters.AddWithValue("$adminOnly", adminOnly ? 1 : 0);
        if (tokenHash is null)
        {
            command.Parameters.AddWithValue("$emailHash", _encryption.LookupHash(NormalizeEmail(email ?? string.Empty)));
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var recruiterId = Guid.Parse(reader.GetString(0));
        await reader.DisposeAsync();

        var recruiter = await GetRecruiterByIdAsync(connection, recruiterId, cancellationToken);
        if (recruiter is null || recruiter.Status is RecruiterStatus.Suspended or RecruiterStatus.Deleted)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var newStatus = recruiter.Status == RecruiterStatus.PendingEmail
            ? recruiter.DomainRisk == DomainRisk.Business ? RecruiterStatus.Active : RecruiterStatus.PendingApproval
            : recruiter.Status;
        var now = _time.GetUtcNow();
        await ExecuteAsync(connection, """
            UPDATE Recruiters SET Status = $status, EmailVerifiedAt = COALESCE(EmailVerifiedAt, $now), LastActiveAt = $now
            WHERE Id = $id;
            """, cancellationToken,
            ("$status", newStatus.ToString()), ("$now", Format(now)), ("$id", recruiterId.ToString()));
        await InsertAuditAsync(connection, recruiterId, "authentication.magic_link_completed", recruiterId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetRecruiterAsync(recruiterId, cancellationToken);
    }

    public async Task<bool> CanProceedWithMailboxProofAsync(string email, int maximumAttempts = 8, CancellationToken cancellationToken = default)
    {
        var emailHash = _encryption.LookupHash(NormalizeEmail(email));
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LoginCodeAttempts (EmailHash, WindowStartedAt, AttemptCount, LockedUntil, UpdatedAt)
            VALUES ($emailHash, $now, 1, NULL, $now)
            ON CONFLICT(EmailHash) DO UPDATE SET
                WindowStartedAt = CASE
                    WHEN LoginCodeAttempts.LockedUntil IS NOT NULL AND LoginCodeAttempts.LockedUntil <= $now THEN $now
                    WHEN LoginCodeAttempts.WindowStartedAt <= $windowCutoff THEN $now
                    ELSE LoginCodeAttempts.WindowStartedAt
                END,
                AttemptCount = CASE
                    WHEN LoginCodeAttempts.LockedUntil IS NOT NULL AND LoginCodeAttempts.LockedUntil <= $now THEN 1
                    WHEN LoginCodeAttempts.WindowStartedAt <= $windowCutoff THEN 1
                    ELSE LoginCodeAttempts.AttemptCount + 1
                END,
                LockedUntil = CASE
                    WHEN LoginCodeAttempts.LockedUntil IS NOT NULL AND LoginCodeAttempts.LockedUntil > $now THEN LoginCodeAttempts.LockedUntil
                    WHEN LoginCodeAttempts.LockedUntil IS NOT NULL AND LoginCodeAttempts.LockedUntil <= $now THEN NULL
                    WHEN LoginCodeAttempts.WindowStartedAt <= $windowCutoff THEN NULL
                    WHEN LoginCodeAttempts.AttemptCount + 1 > $maximumAttempts THEN $lockUntil
                    ELSE NULL
                END,
                UpdatedAt = $now
            RETURNING AttemptCount, LockedUntil;
            """;
        command.Parameters.AddWithValue("$emailHash", emailHash);
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$windowCutoff", Format(now.AddMinutes(-15)));
        command.Parameters.AddWithValue("$maximumAttempts", maximumAttempts);
        command.Parameters.AddWithValue("$lockUntil", Format(now.AddMinutes(15)));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var attempts = reader.GetInt32(0);
        var lockedUntil = reader.IsDBNull(1) ? (DateTimeOffset?)null : Parse(reader.GetString(1));
        return attempts <= maximumAttempts && (lockedUntil is null || lockedUntil <= now);
    }

    public async Task ClearMailboxThrottleAsync(string email, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM LoginCodeAttempts WHERE EmailHash = $emailHash;", cancellationToken,
            ("$emailHash", _encryption.LookupHash(NormalizeEmail(email))));
    }

    public async Task CreateSessionAsync(Guid recruiterId, string sessionHash, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO AuthSessions (SessionHash, RecruiterId, CreatedAt, LastSeenAt, ExpiresAt)
            VALUES ($hash, $recruiterId, $now, $now, $expiresAt);
            """, cancellationToken,
            ("$hash", sessionHash), ("$recruiterId", recruiterId.ToString()), ("$now", Format(now)), ("$expiresAt", Format(expiresAt)));
    }

    public async Task<bool> ValidateAndTouchSessionAsync(Guid recruiterId, string sessionHash, DateTimeOffset newExpiry, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE AuthSessions SET LastSeenAt = $now, ExpiresAt = $newExpiry
            WHERE SessionHash = $hash AND RecruiterId = $recruiterId AND RevokedAt IS NULL AND ExpiresAt > $now
              AND EXISTS (
                  SELECT 1 FROM Recruiters r
                  WHERE r.Id = AuthSessions.RecruiterId AND r.DeletedAt IS NULL AND r.Status NOT IN ('Suspended', 'Deleted')
              )
            RETURNING SessionHash;
            """;
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$newExpiry", Format(newExpiry));
        command.Parameters.AddWithValue("$hash", sessionHash);
        command.Parameters.AddWithValue("$recruiterId", recruiterId.ToString());
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task RevokeSessionAsync(string sessionHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE AuthSessions SET RevokedAt = $now WHERE SessionHash = $hash AND RevokedAt IS NULL;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$hash", sessionHash));
    }

    public async Task RevokeSessionsAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE AuthSessions SET RevokedAt = $now WHERE RecruiterId = $id AND RevokedAt IS NULL;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$id", recruiterId.ToString()));
    }

    public async Task TouchRecruiterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE Recruiters SET LastActiveAt = $now, ExpiryWarningSentAt = NULL WHERE Id = $id;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()));
    }

    public async Task SetRecruiterStatusAsync(Guid id, RecruiterStatus status, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE Recruiters SET Status = $status, LastActiveAt = $now WHERE Id = $id AND IsAdmin = 0;", cancellationToken,
            ("$status", status.ToString()), ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()));
        await InsertAuditAsync(connection, actorId, $"recruiter.status.{status.ToString().ToLowerInvariant()}", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (status != RecruiterStatus.Active)
        {
            await RevokeSessionsAsync(id, cancellationToken);
        }
    }

    public async Task DeleteRecruiterAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var adminCheck = connection.CreateCommand())
        {
            adminCheck.CommandText = "SELECT IsAdmin FROM Recruiters WHERE Id = $id AND DeletedAt IS NULL;";
            adminCheck.Parameters.AddWithValue("$id", id.ToString());
            var isAdmin = await adminCheck.ExecuteScalarAsync(cancellationToken);
            if (isAdmin is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return;
            }
            if (Convert.ToInt32(isAdmin, CultureInfo.InvariantCulture) == 1)
            {
                throw new InvalidOperationException("Administrator accounts must be deprovisioned through the host allowlist and an explicit administrative process.");
            }
        }
        var recruiter = await GetRecruiterByIdAsync(connection, id, cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM Messages WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        await ExecuteAsync(connection, "DELETE FROM ResumeGrants WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        await ExecuteAsync(connection, "DELETE FROM ResumeAccessRequests WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        await ExecuteAsync(connection, "DELETE FROM LoginChallenges WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        await ExecuteAsync(connection, "DELETE FROM AuthSessions WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        await ExecuteAsync(connection, "DELETE FROM AdminTotp WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        await ExecuteAsync(connection, "DELETE FROM AdminTotpAttempts WHERE RecruiterId = $id;", cancellationToken, ("$id", id.ToString()));
        if (recruiter is not null)
        {
            await DeleteMailForRecipientAsync(connection, recruiter.Email, cancellationToken);
            await ExecuteAsync(connection, "DELETE FROM LoginCodeAttempts WHERE EmailHash = $emailHash;", cancellationToken,
                ("$emailHash", _encryption.LookupHash(NormalizeEmail(recruiter.Email))));
        }
        await ExecuteAsync(connection, """
            UPDATE Recruiters SET
                EmailHash = 'deleted:' || Id, EmailEncrypted = '', NameEncrypted = '', OrganisationEncrypted = '', TitleEncrypted = '',
                ProfileUrlEncrypted = '', CountryEncrypted = '', PhoneEncrypted = '', PurposeEncrypted = '',
                Status = 'Deleted', DeletedAt = $now
            WHERE Id = $id AND IsAdmin = 0;
            """, cancellationToken, ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()));
        await InsertAuditAsync(connection, actorId, "recruiter.deleted", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    internal async Task<bool> RecruiterRowExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM Recruiters WHERE Id = $id);";
        command.Parameters.AddWithValue("$id", id.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async Task<byte[]?> GetAdminTotpSecretAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SecretEncrypted FROM AdminTotp WHERE RecruiterId = $id;";
        command.Parameters.AddWithValue("$id", accountId.ToString());
        var value = (string?)await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : _encryption.DecryptBytes(Convert.FromBase64String(value));
    }

    public async Task SaveAdminTotpSecretAsync(Guid accountId, byte[] secret, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO AdminTotp (RecruiterId, SecretEncrypted, EnrolledAt)
            VALUES ($id, $secret, $now)
            ON CONFLICT(RecruiterId) DO UPDATE SET SecretEncrypted = excluded.SecretEncrypted, EnrolledAt = excluded.EnrolledAt;
            """, cancellationToken,
            ("$id", accountId.ToString()), ("$secret", Convert.ToBase64String(_encryption.EncryptBytes(secret))),
            ("$now", Format(_time.GetUtcNow())));
    }

    public async Task<bool> TryBeginAdminTotpAttemptAsync(Guid accountId, int maximumAttempts = 8, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AdminTotpAttempts (RecruiterId, WindowStartedAt, AttemptCount, LockedUntil)
            VALUES ($id, $now, 1, NULL)
            ON CONFLICT(RecruiterId) DO UPDATE SET
                WindowStartedAt = CASE
                    WHEN AdminTotpAttempts.LockedUntil IS NOT NULL AND AdminTotpAttempts.LockedUntil <= $now THEN $now
                    WHEN AdminTotpAttempts.WindowStartedAt <= $windowCutoff THEN $now
                    ELSE AdminTotpAttempts.WindowStartedAt
                END,
                AttemptCount = CASE
                    WHEN AdminTotpAttempts.LockedUntil IS NOT NULL AND AdminTotpAttempts.LockedUntil <= $now THEN 1
                    WHEN AdminTotpAttempts.WindowStartedAt <= $windowCutoff THEN 1
                    ELSE AdminTotpAttempts.AttemptCount + 1
                END,
                LockedUntil = CASE
                    WHEN AdminTotpAttempts.LockedUntil IS NOT NULL AND AdminTotpAttempts.LockedUntil > $now THEN AdminTotpAttempts.LockedUntil
                    WHEN AdminTotpAttempts.LockedUntil IS NOT NULL AND AdminTotpAttempts.LockedUntil <= $now THEN NULL
                    WHEN AdminTotpAttempts.WindowStartedAt <= $windowCutoff THEN NULL
                    WHEN AdminTotpAttempts.AttemptCount + 1 > $maximumAttempts THEN $lockUntil
                    ELSE NULL
                END
            RETURNING AttemptCount, LockedUntil;
            """;
        command.Parameters.AddWithValue("$id", accountId.ToString());
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$windowCutoff", Format(now.AddMinutes(-15)));
        command.Parameters.AddWithValue("$maximumAttempts", maximumAttempts);
        command.Parameters.AddWithValue("$lockUntil", Format(now.AddMinutes(15)));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        var attempts = reader.GetInt32(0);
        var lockedUntil = reader.IsDBNull(1) ? (DateTimeOffset?)null : Parse(reader.GetString(1));
        await reader.DisposeAsync();
        var allowed = attempts <= maximumAttempts && (lockedUntil is null || lockedUntil <= now);
        if (!allowed && attempts == maximumAttempts + 1)
        {
            await InsertAuditAsync(connection, accountId, "authentication.totp_throttled", accountId.ToString(), cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return allowed;
    }

    public async Task ClearAdminTotpAttemptsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM AdminTotpAttempts WHERE RecruiterId = $id;", cancellationToken,
            ("$id", accountId.ToString()));
    }

    internal async Task<bool> HasAdminTotpAttemptStateAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM AdminTotpAttempts WHERE RecruiterId = $id);";
        command.Parameters.AddWithValue("$id", accountId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async Task AddMessageAsync(Guid recruiterId, string subject, string body, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO Messages (Id, RecruiterId, SubjectEncrypted, BodyEncrypted, CreatedAt)
            VALUES ($id, $recruiterId, $subject, $body, $now);
            """, cancellationToken,
            ("$id", id.ToString()), ("$recruiterId", recruiterId.ToString()), ("$subject", _encryption.Encrypt(subject)),
            ("$body", _encryption.Encrypt(body)), ("$now", Format(_time.GetUtcNow())));
        await InsertAuditAsync(connection, recruiterId, "message.created", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> TryQueueResumeAccessRequestAsync(Guid recruiterId, string? administratorEmail, CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ResumeAccessRequests (RecruiterId, LastRequestedAt)
            VALUES ($id, $now)
            ON CONFLICT(RecruiterId) DO UPDATE SET LastRequestedAt = excluded.LastRequestedAt
            WHERE ResumeAccessRequests.LastRequestedAt <= $cooldown
            RETURNING RecruiterId;
            """;
        command.Parameters.AddWithValue("$id", recruiterId.ToString());
        command.Parameters.AddWithValue("$now", Format(now));
        command.Parameters.AddWithValue("$cooldown", Format(now.AddHours(-24)));
        if (await command.ExecuteScalarAsync(cancellationToken) is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        var messageId = Guid.NewGuid();
        await ExecuteAsync(connection, """
            INSERT INTO Messages (Id, RecruiterId, SubjectEncrypted, BodyEncrypted, CreatedAt)
            VALUES ($id, $recruiterId, $subject, $body, $now);
            """, cancellationToken,
            ("$id", messageId.ToString()), ("$recruiterId", recruiterId.ToString()),
            ("$subject", _encryption.Encrypt("Résumé access requested")),
            ("$body", _encryption.Encrypt("Please review this verified recruiter account for a 30-day résumé download grant.")),
            ("$now", Format(now)));
        await InsertAuditAsync(connection, recruiterId, "resume.requested", recruiterId.ToString(), cancellationToken);

        if (!string.IsNullOrWhiteSpace(administratorEmail))
        {
            await ExecuteAsync(connection, """
                INSERT INTO MailOutbox (Id, Kind, RecipientEncrypted, SubjectEncrypted, BodyEncrypted, Status, AttemptCount, NextAttemptAt, CreatedAt)
                VALUES ($id, 'resume-request', $recipient, $subject, $body, 'Pending', 0, $now, $now);
                """, cancellationToken,
                ("$id", Guid.NewGuid().ToString()), ("$recipient", _encryption.Encrypt(administratorEmail)),
                ("$subject", _encryption.Encrypt("Résumé access requested on reneb.au")),
                ("$body", _encryption.Encrypt("<p>A verified recruiter requested résumé access. Sign in to the administrator portal to review it.</p>")),
                ("$now", Format(now)));
        }
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<List<MessageRecord>> ListMessagesAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<MessageRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT m.Id, m.RecruiterId, r.NameEncrypted, r.OrganisationEncrypted, m.SubjectEncrypted,
                   m.BodyEncrypted, m.CreatedAt, m.ReadAt
            FROM Messages m JOIN Recruiters r ON r.Id = m.RecruiterId
            ORDER BY m.CreatedAt DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new MessageRecord(
                Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), _encryption.Decrypt(reader.GetString(2)),
                _encryption.Decrypt(reader.GetString(3)), _encryption.Decrypt(reader.GetString(4)), _encryption.Decrypt(reader.GetString(5)),
                Parse(reader.GetString(6)), reader.IsDBNull(7) ? null : Parse(reader.GetString(7))));
        }
        return result;
    }

    public async Task MarkMessageReadAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE Messages SET ReadAt = COALESCE(ReadAt, $now) WHERE Id = $id;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()));
        await InsertAuditAsync(connection, actorId, "message.read", id.ToString(), cancellationToken);
    }

    public async Task DeleteMessageAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM Messages WHERE Id = $id;", cancellationToken, ("$id", id.ToString()));
        await InsertAuditAsync(connection, actorId, "message.deleted", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ResumeRecord> SaveResumeAsync(string originalFileName, byte[] content, Guid actorId, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var now = _time.GetUtcNow();
        var sha = Convert.ToHexString(SHA256.HashData(content));
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE ResumeVersions SET RetireAfter = COALESCE(RetireAfter, $retire) WHERE IsActive = 0;", cancellationToken,
            ("$retire", Format(now.AddDays(30))));
        await ExecuteAsync(connection, "UPDATE ResumeVersions SET IsActive = 0, RetireAfter = NULL WHERE IsActive = 1;", cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO ResumeVersions (Id, OriginalFileNameEncrypted, ContentEncrypted, Sha256, Size, UploadedAt, IsActive)
            VALUES ($id, $name, $content, $sha, $size, $now, 1);
            """, cancellationToken,
            ("$id", id.ToString()), ("$name", _encryption.Encrypt(originalFileName)), ("$content", _encryption.EncryptBytes(content)),
            ("$sha", sha), ("$size", content.LongLength), ("$now", Format(now)));
        await InsertAuditAsync(connection, actorId, "resume.uploaded", id.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new ResumeRecord(id, originalFileName, sha, content.LongLength, now, true);
    }

    public async Task<ResumeRecord?> GetActiveResumeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, OriginalFileNameEncrypted, Sha256, Size, UploadedAt, IsActive FROM ResumeVersions WHERE IsActive = 1 LIMIT 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ResumeRecord(Guid.Parse(reader.GetString(0)), _encryption.Decrypt(reader.GetString(1)), reader.GetString(2), reader.GetInt64(3), Parse(reader.GetString(4)), reader.GetBoolean(5))
            : null;
    }

    public async Task<(ResumeRecord Record, byte[] Content)?> GetActiveResumeContentForAdminAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, OriginalFileNameEncrypted, ContentEncrypted, Sha256, Size, UploadedAt, IsActive FROM ResumeVersions WHERE IsActive = 1 LIMIT 1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        var record = new ResumeRecord(Guid.Parse(reader.GetString(0)), _encryption.Decrypt(reader.GetString(1)), reader.GetString(3), reader.GetInt64(4), Parse(reader.GetString(5)), reader.GetBoolean(6));
        return (record, _encryption.DecryptBytes((byte[])reader[2]));
    }

    internal async Task<List<Guid>> GetResumeVersionIdsAsync(CancellationToken cancellationToken = default)
    {
        var ids = new List<Guid>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM ResumeVersions ORDER BY UploadedAt DESC;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }
        return ids;
    }

    public async Task<(ResumeRecord Record, byte[] Content)?> GetResumeForRecruiterAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT rv.Id, rv.OriginalFileNameEncrypted, rv.ContentEncrypted, rv.Sha256, rv.Size, rv.UploadedAt, rv.IsActive
            FROM ResumeVersions rv
            JOIN ResumeGrants rg ON rg.ResumeId = rv.Id
            WHERE rg.RecruiterId = $recruiterId AND rg.RevokedAt IS NULL AND rg.ExpiresAt > $now AND rv.IsActive = 1
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$recruiterId", recruiterId.ToString());
        command.Parameters.AddWithValue("$now", Format(_time.GetUtcNow()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        var record = new ResumeRecord(Guid.Parse(reader.GetString(0)), _encryption.Decrypt(reader.GetString(1)), reader.GetString(3), reader.GetInt64(4), Parse(reader.GetString(5)), reader.GetBoolean(6));
        return (record, _encryption.DecryptBytes((byte[])reader[2]));
    }

    public async Task<ResumeGrantRecord?> GetResumeGrantAsync(Guid recruiterId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ResumeId, GrantedAt, ExpiresAt, RevokedAt FROM ResumeGrants WHERE RecruiterId = $id;";
        command.Parameters.AddWithValue("$id", recruiterId.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new ResumeGrantRecord(recruiterId, Guid.Parse(reader.GetString(0)), Parse(reader.GetString(1)), Parse(reader.GetString(2)), reader.IsDBNull(3) ? null : Parse(reader.GetString(3)))
            : null;
    }

    public async Task GrantResumeAsync(Guid recruiterId, Guid actorId, CancellationToken cancellationToken = default)
    {
        var recruiter = await GetRecruiterAsync(recruiterId, cancellationToken);
        if (recruiter?.Status != RecruiterStatus.Active)
        {
            throw new InvalidOperationException("Only an active recruiter can receive résumé access.");
        }
        var resume = await GetActiveResumeAsync(cancellationToken) ?? throw new InvalidOperationException("No active résumé is available.");
        var now = _time.GetUtcNow();
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO ResumeGrants (RecruiterId, ResumeId, GrantedAt, ExpiresAt, RevokedAt)
            VALUES ($recruiterId, $resumeId, $now, $expires, NULL)
            ON CONFLICT(RecruiterId) DO UPDATE SET ResumeId = excluded.ResumeId, GrantedAt = excluded.GrantedAt,
                ExpiresAt = excluded.ExpiresAt, RevokedAt = NULL;
            """, cancellationToken,
            ("$recruiterId", recruiterId.ToString()), ("$resumeId", resume.Id.ToString()), ("$now", Format(now)),
            ("$expires", Format(now.AddDays(30))));
        await ExecuteAsync(connection, "DELETE FROM ResumeAccessRequests WHERE RecruiterId = $recruiterId;", cancellationToken,
            ("$recruiterId", recruiterId.ToString()));
        await InsertAuditAsync(connection, actorId, "resume.granted", recruiterId.ToString(), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RevokeResumeAsync(Guid recruiterId, Guid actorId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE ResumeGrants SET RevokedAt = $now WHERE RecruiterId = $id;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$id", recruiterId.ToString()));
        await InsertAuditAsync(connection, actorId, "resume.revoked", recruiterId.ToString(), cancellationToken);
    }

    public async Task RecordResumeDownloadAsync(Guid recruiterId, Guid resumeId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await InsertAuditAsync(connection, recruiterId, "resume.downloaded", resumeId.ToString(), cancellationToken);
    }

    internal async Task<bool> HasAuditEventAsync(string eventType, Guid actorId, Guid targetId, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM AuditEvents
                WHERE EventType = $eventType AND ActorId = $actorId AND TargetId = $targetId
            );
            """;
        command.Parameters.AddWithValue("$eventType", eventType);
        command.Parameters.AddWithValue("$actorId", actorId.ToString());
        command.Parameters.AddWithValue("$targetId", targetId.ToString());
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    public async Task EnqueueMailAsync(string kind, string recipient, string subject, string body, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO MailOutbox (Id, Kind, RecipientEncrypted, SubjectEncrypted, BodyEncrypted, Status, AttemptCount, NextAttemptAt, CreatedAt)
            VALUES ($id, $kind, $recipient, $subject, $body, 'Pending', 0, $now, $now);
            """, cancellationToken,
            ("$id", Guid.NewGuid().ToString()), ("$kind", kind), ("$recipient", _encryption.Encrypt(recipient)),
            ("$subject", _encryption.Encrypt(subject)), ("$body", _encryption.Encrypt(body)), ("$now", Format(_time.GetUtcNow())));
    }

    public async Task<OutboxRecord?> GetDueMailAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Kind, RecipientEncrypted, SubjectEncrypted, BodyEncrypted, AttemptCount
            FROM MailOutbox WHERE Status = 'Pending' AND NextAttemptAt <= $now ORDER BY CreatedAt LIMIT 1;
            """;
        command.Parameters.AddWithValue("$now", Format(_time.GetUtcNow()));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new OutboxRecord(Guid.Parse(reader.GetString(0)), reader.GetString(1), _encryption.Decrypt(reader.GetString(2)),
                _encryption.Decrypt(reader.GetString(3)), _encryption.Decrypt(reader.GetString(4)), reader.GetInt32(5))
            : null;
    }

    public async Task<OutboxRecord?> FindPendingMailForRecipientAsync(string recipient, string? kind = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Kind, RecipientEncrypted, SubjectEncrypted, BodyEncrypted, AttemptCount
            FROM MailOutbox
            WHERE Status = 'Pending' AND ($kind IS NULL OR Kind = $kind)
            ORDER BY CreatedAt DESC, Id DESC;
            """;
        command.Parameters.AddWithValue("$kind", (object?)kind ?? DBNull.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var decryptedRecipient = _encryption.Decrypt(reader.GetString(2));
            if (string.Equals(decryptedRecipient, recipient, StringComparison.OrdinalIgnoreCase))
            {
                return new OutboxRecord(Guid.Parse(reader.GetString(0)), reader.GetString(1), decryptedRecipient,
                    _encryption.Decrypt(reader.GetString(3)), _encryption.Decrypt(reader.GetString(4)), reader.GetInt32(5));
            }
        }
        return null;
    }

    public async Task MarkMailSentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "UPDATE MailOutbox SET Status = 'Sent', SentAt = $now, LastErrorCode = NULL WHERE Id = $id;", cancellationToken,
            ("$now", Format(_time.GetUtcNow())), ("$id", id.ToString()));
    }

    public async Task MarkMailFailedAsync(Guid id, int priorAttempts, string errorCode, CancellationToken cancellationToken = default)
    {
        var attempts = priorAttempts + 1;
        var delay = TimeSpan.FromMinutes(Math.Min(60, Math.Pow(2, attempts)));
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            UPDATE MailOutbox SET AttemptCount = $attempts, NextAttemptAt = $next,
                Status = CASE WHEN $attempts >= 8 THEN 'DeadLetter' ELSE 'Pending' END, LastErrorCode = $error
            WHERE Id = $id;
            """, cancellationToken,
            ("$attempts", attempts), ("$next", Format(_time.GetUtcNow().Add(delay))), ("$error", errorCode), ("$id", id.ToString()));
    }

    public async Task CaptureDevelopmentMailAsync(OutboxRecord record, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await ExecuteAsync(connection, """
            INSERT INTO DevelopmentMail (Id, RecipientEncrypted, SubjectEncrypted, BodyEncrypted, CreatedAt)
            VALUES ($id, $recipient, $subject, $body, $now);
            """, cancellationToken,
            ("$id", record.Id.ToString()), ("$recipient", _encryption.Encrypt(record.Recipient)),
            ("$subject", _encryption.Encrypt(record.Subject)), ("$body", _encryption.Encrypt(record.Body)),
            ("$now", Format(_time.GetUtcNow())));
    }

    public async Task<List<OutboxRecord>> ListDevelopmentMailAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<OutboxRecord>();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, RecipientEncrypted, SubjectEncrypted, BodyEncrypted FROM DevelopmentMail ORDER BY CreatedAt DESC LIMIT 20;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new OutboxRecord(Guid.Parse(reader.GetString(0)), "Development", _encryption.Decrypt(reader.GetString(1)),
                _encryption.Decrypt(reader.GetString(2)), _encryption.Decrypt(reader.GetString(3)), 0));
        }
        return result;
    }

    public async Task RunRetentionAsync(CancellationToken cancellationToken = default)
    {
        var now = _time.GetUtcNow();
        var cutoff = now.AddDays(-180);
        var auditCutoff = now.AddDays(-365);
        var warningCutoff = now.AddDays(-150);
        var mailCutoff = now.AddDays(-30);
        var warnings = new List<(Guid Id, string Email)>();
        var expired = new List<Guid>();

        await using var connection = await OpenAsync(cancellationToken);
        await using (var warningCommand = connection.CreateCommand())
        {
            warningCommand.CommandText = """
                SELECT * FROM Recruiters
                WHERE IsAdmin = 0 AND DeletedAt IS NULL AND LastActiveAt < $warningCutoff AND ExpiryWarningSentAt IS NULL;
                """;
            warningCommand.Parameters.AddWithValue("$warningCutoff", Format(warningCutoff));
            await using var reader = await warningCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var record = ReadRecruiter(reader);
                warnings.Add((record.Id, record.Email));
            }
        }
        await using (var expiryCommand = connection.CreateCommand())
        {
            expiryCommand.CommandText = "SELECT Id FROM Recruiters WHERE IsAdmin = 0 AND DeletedAt IS NULL AND LastActiveAt < $cutoff;";
            expiryCommand.Parameters.AddWithValue("$cutoff", Format(cutoff));
            await using var reader = await expiryCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) expired.Add(Guid.Parse(reader.GetString(0)));
        }

        foreach (var warning in warnings)
        {
            await EnqueueMailAsync("retention-warning", warning.Email, "Your reneb.au recruiter access will expire",
                "<p>Your recruiter portal account has been inactive and will be deleted in 30 days unless you sign in again. You can also sign in now and delete it immediately.</p>", cancellationToken);
            await ExecuteAsync(connection, "UPDATE Recruiters SET ExpiryWarningSentAt = $now WHERE Id = $id;", cancellationToken,
                ("$now", Format(now)), ("$id", warning.Id.ToString()));
        }
        foreach (var id in expired)
        {
            await DeleteRecruiterAsync(id, Guid.Empty, cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, "DELETE FROM Messages WHERE RecruiterId IN (SELECT Id FROM Recruiters WHERE IsAdmin = 0 AND LastActiveAt < $cutoff);", cancellationToken,
            ("$cutoff", Format(cutoff)));
        await ExecuteAsync(connection, "DELETE FROM ResumeGrants WHERE ExpiresAt <= $now OR RevokedAt IS NOT NULL;", cancellationToken,
            ("$now", Format(now)));
        await ExecuteAsync(connection, "DELETE FROM ResumeAccessRequests WHERE LastRequestedAt < $cutoff;", cancellationToken,
            ("$cutoff", Format(cutoff)));
        await ExecuteAsync(connection, "DELETE FROM LoginChallenges WHERE ExpiresAt < $cutoff;", cancellationToken,
            ("$cutoff", Format(now.AddDays(-7))));
        await ExecuteAsync(connection, "DELETE FROM LoginCodeAttempts WHERE UpdatedAt < $cutoff;", cancellationToken,
            ("$cutoff", Format(now.AddDays(-7))));
        await ExecuteAsync(connection, "DELETE FROM AuthSessions WHERE ExpiresAt < $now OR RevokedAt IS NOT NULL;", cancellationToken,
            ("$now", Format(now)));
        await ExecuteAsync(connection, "DELETE FROM MailOutbox WHERE CreatedAt < $mailCutoff;", cancellationToken,
            ("$mailCutoff", Format(mailCutoff)));
        await ExecuteAsync(connection, "DELETE FROM DevelopmentMail WHERE CreatedAt < $mailCutoff;", cancellationToken,
            ("$mailCutoff", Format(mailCutoff)));
        await ExecuteAsync(connection, "DELETE FROM AiConversations WHERE LastActiveAt < $cutoff;", cancellationToken,
            ("$cutoff", Format(now.AddDays(-_aiOptions.ConversationRetentionDays))));
        await ExecuteAsync(connection, "UPDATE AiUsageLedger SET Status = 'Failed', CompletedAt = $now WHERE Status = 'Reserved' AND CreatedAt < $cutoff;", cancellationToken,
            ("$now", Format(now)), ("$cutoff", Format(now.AddHours(-1))));
        await ExecuteAsync(connection, "DELETE FROM AuditEvents WHERE OccurredAt < $auditCutoff;", cancellationToken,
            ("$auditCutoff", Format(auditCutoff)));
        await ExecuteAsync(connection, "DELETE FROM Recruiters WHERE IsAdmin = 0 AND DeletedAt IS NOT NULL AND DeletedAt < $auditCutoff;", cancellationToken,
            ("$auditCutoff", Format(auditCutoff)));
        await ExecuteAsync(connection, """
            DELETE FROM ResumeVersions
            WHERE IsActive = 0 AND RetireAfter < $now
              AND Id NOT IN (
                  SELECT Id FROM ResumeVersions WHERE IsActive = 0 ORDER BY UploadedAt DESC LIMIT 1
              );
            """, cancellationToken,
            ("$now", Format(now)));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<string> BackupAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_backupDirectory);
        var timestamp = _time.GetUtcNow().ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var temp = Path.Combine(Path.GetTempPath(), $"reneb-au-{Guid.NewGuid():N}.sqlite3");
        var output = Path.Combine(_backupDirectory, $"recruiter-portal-{timestamp}.sqlite3.enc");
        try
        {
            await using var source = await OpenAsync(cancellationToken);
            await using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temp }.ToString());
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
            var plaintext = await File.ReadAllBytesAsync(temp, cancellationToken);
            await File.WriteAllBytesAsync(output, _encryption.EncryptBytes(plaintext), cancellationToken);
            CryptographicOperations.ZeroMemory(plaintext);
            return output;
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    public async Task<bool> RestoreCheckAsync(string path, CancellationToken cancellationToken = default)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"reneb-au-restore-{Guid.NewGuid():N}.sqlite3");
        try
        {
            var plaintext = _encryption.DecryptBytes(await File.ReadAllBytesAsync(path, cancellationToken));
            await File.WriteAllBytesAsync(temp, plaintext, cancellationToken);
            CryptographicOperations.ZeroMemory(plaintext);
            var builder = new SqliteConnectionStringBuilder { DataSource = temp, Mode = SqliteOpenMode.ReadOnly };
            await using var connection = new SqliteConnection(builder.ToString());
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            return string.Equals((string?)await command.ExecuteScalarAsync(cancellationToken), "ok", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await ExecuteAsync(connection, "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;", cancellationToken);
        return connection;
    }

    private async Task<RecruiterRecord?> FindRecruiterByHashAsync(SqliteConnection connection, string hash, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Recruiters WHERE EmailHash = $hash AND DeletedAt IS NULL;";
        command.Parameters.AddWithValue("$hash", hash);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecruiter(reader) : null;
    }

    private async Task<RecruiterRecord?> GetRecruiterByIdAsync(SqliteConnection connection, Guid id, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Recruiters WHERE Id = $id AND DeletedAt IS NULL;";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRecruiter(reader) : null;
    }

    private async Task DeleteMailForRecipientAsync(SqliteConnection connection, string recipient, CancellationToken cancellationToken)
    {
        foreach (var table in new[] { "MailOutbox", "DevelopmentMail" })
        {
            var ids = new List<string>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = $"SELECT Id, RecipientEncrypted FROM {table};";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (string.Equals(_encryption.Decrypt(reader.GetString(1)), recipient, StringComparison.OrdinalIgnoreCase))
                    {
                        ids.Add(reader.GetString(0));
                    }
                }
            }
            foreach (var id in ids)
            {
                await ExecuteAsync(connection, $"DELETE FROM {table} WHERE Id = $id;", cancellationToken, ("$id", id));
            }
        }
    }

    private RecruiterRecord ReadRecruiter(SqliteDataReader reader) => new(
        Guid.Parse(reader.GetString(reader.GetOrdinal("Id"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("EmailEncrypted"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("NameEncrypted"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("OrganisationEncrypted"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("TitleEncrypted"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("ProfileUrlEncrypted"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("CountryEncrypted"))),
        string.IsNullOrEmpty(_encryption.Decrypt(reader.GetString(reader.GetOrdinal("PhoneEncrypted")))) ? null : _encryption.Decrypt(reader.GetString(reader.GetOrdinal("PhoneEncrypted"))),
        _encryption.Decrypt(reader.GetString(reader.GetOrdinal("PurposeEncrypted"))),
        Enum.Parse<RecruiterStatus>(reader.GetString(reader.GetOrdinal("Status"))),
        Enum.Parse<DomainRisk>(reader.GetString(reader.GetOrdinal("DomainRisk"))),
        Parse(reader.GetString(reader.GetOrdinal("CreatedAt"))),
        reader.IsDBNull(reader.GetOrdinal("EmailVerifiedAt")) ? null : Parse(reader.GetString(reader.GetOrdinal("EmailVerifiedAt"))),
        Parse(reader.GetString(reader.GetOrdinal("LastActiveAt"))));

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken, params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value ?? DBNull.Value);
        }
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertAuditAsync(SqliteConnection connection, Guid? actorId, string eventType, string targetId, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, """
            INSERT INTO AuditEvents (Id, ActorId, EventType, TargetId, OccurredAt)
            VALUES ($id, $actorId, $eventType, $targetId, $now);
            """, cancellationToken,
            ("$id", Guid.NewGuid().ToString()), ("$actorId", actorId?.ToString()), ("$eventType", eventType),
            ("$targetId", targetId), ("$now", Format(_time.GetUtcNow())));
    }

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
    private static string Format(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public sealed record OutboxRecord(Guid Id, string Kind, string Recipient, string Subject, string Body, int AttemptCount);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS SchemaVersions (
            Version INTEGER PRIMARY KEY,
            AppliedAt TEXT NOT NULL
        );
        INSERT OR IGNORE INTO SchemaVersions (Version, AppliedAt) VALUES (1, strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));

        CREATE TABLE IF NOT EXISTS CandidateProfiles (
            Id INTEGER PRIMARY KEY CHECK (Id = 1),
            DraftPublicJson TEXT NOT NULL,
            PublishedPublicJson TEXT NOT NULL,
            PrivateEncrypted TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            PublishedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Recruiters (
            Id TEXT PRIMARY KEY,
            EmailHash TEXT NOT NULL UNIQUE,
            EmailEncrypted TEXT NOT NULL,
            NameEncrypted TEXT NOT NULL,
            OrganisationEncrypted TEXT NOT NULL,
            TitleEncrypted TEXT NOT NULL,
            ProfileUrlEncrypted TEXT NOT NULL,
            CountryEncrypted TEXT NOT NULL,
            PhoneEncrypted TEXT NOT NULL,
            PurposeEncrypted TEXT NOT NULL,
            DomainRisk TEXT NOT NULL,
            Status TEXT NOT NULL,
            IsAdmin INTEGER NOT NULL DEFAULT 0,
            CreatedAt TEXT NOT NULL,
            EmailVerifiedAt TEXT NULL,
            LastActiveAt TEXT NOT NULL,
            ExpiryWarningSentAt TEXT NULL,
            DeletedAt TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS LoginChallenges (
            Id TEXT PRIMARY KEY,
            RecruiterId TEXT NOT NULL REFERENCES Recruiters(Id) ON DELETE CASCADE,
            TokenHash TEXT NOT NULL UNIQUE,
            CodeHash TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            ConsumedAt TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_LoginChallenges_CodeHash ON LoginChallenges(CodeHash);

        CREATE TABLE IF NOT EXISTS LoginCodeAttempts (
            EmailHash TEXT PRIMARY KEY,
            WindowStartedAt TEXT NOT NULL,
            AttemptCount INTEGER NOT NULL,
            LockedUntil TEXT NULL,
            UpdatedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS AuthSessions (
            SessionHash TEXT PRIMARY KEY,
            RecruiterId TEXT NOT NULL REFERENCES Recruiters(Id) ON DELETE CASCADE,
            CreatedAt TEXT NOT NULL,
            LastSeenAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            RevokedAt TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_AuthSessions_RecruiterId ON AuthSessions(RecruiterId);

        CREATE TABLE IF NOT EXISTS AdminTotp (
            RecruiterId TEXT PRIMARY KEY REFERENCES Recruiters(Id) ON DELETE CASCADE,
            SecretEncrypted TEXT NOT NULL,
            EnrolledAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS AdminTotpAttempts (
            RecruiterId TEXT PRIMARY KEY REFERENCES Recruiters(Id) ON DELETE CASCADE,
            WindowStartedAt TEXT NOT NULL,
            AttemptCount INTEGER NOT NULL,
            LockedUntil TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS ResumeVersions (
            Id TEXT PRIMARY KEY,
            OriginalFileNameEncrypted TEXT NOT NULL,
            ContentEncrypted BLOB NOT NULL,
            Sha256 TEXT NOT NULL,
            Size INTEGER NOT NULL,
            UploadedAt TEXT NOT NULL,
            IsActive INTEGER NOT NULL,
            RetireAfter TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS ResumeGrants (
            RecruiterId TEXT PRIMARY KEY REFERENCES Recruiters(Id) ON DELETE CASCADE,
            ResumeId TEXT NOT NULL REFERENCES ResumeVersions(Id) ON DELETE CASCADE,
            GrantedAt TEXT NOT NULL,
            ExpiresAt TEXT NOT NULL,
            RevokedAt TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS ResumeAccessRequests (
            RecruiterId TEXT PRIMARY KEY REFERENCES Recruiters(Id) ON DELETE CASCADE,
            LastRequestedAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS Messages (
            Id TEXT PRIMARY KEY,
            RecruiterId TEXT NOT NULL REFERENCES Recruiters(Id) ON DELETE CASCADE,
            SubjectEncrypted TEXT NOT NULL,
            BodyEncrypted TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            ReadAt TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS AuditEvents (
            Id TEXT PRIMARY KEY,
            ActorId TEXT NULL,
            EventType TEXT NOT NULL,
            TargetId TEXT NOT NULL,
            OccurredAt TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS MailOutbox (
            Id TEXT PRIMARY KEY,
            Kind TEXT NOT NULL,
            RecipientEncrypted TEXT NOT NULL,
            SubjectEncrypted TEXT NOT NULL,
            BodyEncrypted TEXT NOT NULL,
            Status TEXT NOT NULL,
            AttemptCount INTEGER NOT NULL,
            NextAttemptAt TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            SentAt TEXT NULL,
            LastErrorCode TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS DevelopmentMail (
            Id TEXT PRIMARY KEY,
            RecipientEncrypted TEXT NOT NULL,
            SubjectEncrypted TEXT NOT NULL,
            BodyEncrypted TEXT NOT NULL,
            CreatedAt TEXT NOT NULL
        );
        """;
}

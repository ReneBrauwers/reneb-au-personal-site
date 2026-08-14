using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Security;

public sealed class IdentityService
{
    private readonly PortalDatabase _database;
    private readonly FieldEncryptionService _encryption;
    private readonly PortalOptions _options;
    private readonly TimeProvider _time;

    public IdentityService(
        PortalDatabase database,
        FieldEncryptionService encryption,
        IOptions<PortalOptions> options,
        TimeProvider time)
    {
        _database = database;
        _encryption = encryption;
        _options = options.Value;
        _time = time;
    }

    public async Task<bool> RequestRegistrationAsync(RecruiterRegistration registration, CancellationToken cancellationToken)
    {
        var risk = DomainRiskClassifier.Classify(
            registration.Email,
            _options.UntrustedEmailDomains,
            _options.DisposableEmailDomains);
        if (risk == DomainRisk.Disposable)
        {
            return false;
        }

        var recruiter = await _database.UpsertPendingRecruiterAsync(registration, risk, cancellationToken);
        if (recruiter.Status is RecruiterStatus.Suspended or RecruiterStatus.Deleted)
        {
            return true;
        }
        await CreateChallengeAndMailAsync(recruiter, cancellationToken);
        return true;
    }

    public async Task RequestLoginAsync(string email, bool adminOnly, CancellationToken cancellationToken)
    {
        var normalized = PortalDatabase.NormalizeEmail(email);
        RecruiterRecord? recruiter;
        if (adminOnly)
        {
            if (!IsAdminEmail(normalized))
            {
                return;
            }
            recruiter = await _database.EnsureAdminAccountAsync(normalized, cancellationToken);
        }
        else
        {
            recruiter = await _database.FindRecruiterByEmailAsync(normalized, cancellationToken);
        }

        if (recruiter is null || recruiter.Status is RecruiterStatus.Suspended or RecruiterStatus.Deleted)
        {
            return;
        }

        await CreateChallengeAndMailAsync(recruiter, cancellationToken);
    }

    public async Task<RecruiterRecord?> CompleteTokenAsync(string token, CancellationToken cancellationToken, bool adminOnly = false)
        => await _database.ConsumeLoginChallengeAsync(HashToken(token), null, null, adminOnly, cancellationToken);

    public async Task<RecruiterRecord?> CompleteCodeAsync(string email, string code, CancellationToken cancellationToken, bool adminOnly = false)
    {
        var normalized = PortalDatabase.NormalizeEmail(email);
        if (adminOnly && !IsAdminEmail(normalized))
        {
            return null;
        }
        if (!await _database.CanProceedWithMailboxProofAsync(normalized, 8, cancellationToken))
        {
            return null;
        }
        var codeHash = _encryption.LookupHash($"{normalized}:{NormalizeCode(code)}");
        var recruiter = await _database.ConsumeLoginChallengeAsync(null, normalized, codeHash, adminOnly, cancellationToken);
        if (recruiter is not null)
        {
            await _database.ClearMailboxThrottleAsync(normalized, cancellationToken);
        }
        return recruiter;
    }

    public async Task SignInAsync(HttpContext context, RecruiterRecord recruiter, bool totpVerified = false)
    {
        var now = _time.GetUtcNow();
        if (context.User.FindFirstValue("session_id") is { Length: > 0 } previousSession)
        {
            await _database.RevokeSessionAsync(HashSession(previousSession));
        }
        var sessionToken = Base64Url(RandomNumberGenerator.GetBytes(32));
        await _database.CreateSessionAsync(recruiter.Id, HashSession(sessionToken), now.AddMinutes(30));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, recruiter.Id.ToString()),
            new(ClaimTypes.Email, recruiter.Email),
            new(ClaimTypes.Name, recruiter.Name),
            new("auth_time", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("session_id", sessionToken)
        };
        if (totpVerified)
        {
            claims.Add(new Claim("totp_at", now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        {
            AllowRefresh = true,
            IsPersistent = false,
            IssuedUtc = now,
            ExpiresUtc = now.AddMinutes(30)
        });
    }

    public bool IsAdminEmail(string email) => _options.AdminEmails
        .Select(PortalDatabase.NormalizeEmail)
        .Contains(PortalDatabase.NormalizeEmail(email), StringComparer.Ordinal);

    public static Guid CurrentUserId(ClaimsPrincipal principal)
        => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated account identifier is missing."));

    public bool HasRecentTotp(ClaimsPrincipal principal, TimeSpan maximumAge)
    {
        if (!long.TryParse(principal.FindFirstValue("totp_at"), out var timestamp))
        {
            return false;
        }
        return _time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(timestamp) <= maximumAge;
    }

    private async Task CreateChallengeAndMailAsync(RecruiterRecord recruiter, CancellationToken cancellationToken)
    {
        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var code = CreateCode();
        var normalized = PortalDatabase.NormalizeEmail(recruiter.Email);
        var created = await _database.CreateLoginChallengeAsync(
            recruiter.Id,
            HashToken(token),
            _encryption.LookupHash($"{normalized}:{code}"),
            _time.GetUtcNow().AddMinutes(15),
            _options.AuthRequestsPerMinute,
            cancellationToken);
        if (!created)
        {
            return;
        }

        var link = $"{_options.CanonicalBaseUrl.TrimEnd('/')}/auth/complete#token={Uri.EscapeDataString(token)}";
        var body = $"""
            <p>Use this private link to continue to René Brauwers' recruiter portal. It expires in 15 minutes and can be used once.</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(link)}">Continue to the recruiter portal</a></p>
            <p>If the link cannot be opened, enter this eight-character email verification code on the completion page: <strong>{code}</strong></p>
            <p>This code verifies mailbox access only. Administrators will then be asked for a separate six-digit number from their authenticator app.</p>
            <p>If you did not request this message, no action is required.</p>
            """;
        await _database.EnqueueMailAsync("magic-link", recruiter.Email, "Your secure reneb.au sign-in link", body, cancellationToken);
    }

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));

    public static string HashSession(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string CreateCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        return new string(bytes.Select(value => alphabet[value % alphabet.Length]).ToArray());
    }

    private static string NormalizeCode(string code)
        => new(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

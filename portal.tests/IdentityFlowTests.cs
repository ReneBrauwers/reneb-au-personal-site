using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Pages.Admin;
using ReneB.Portal.Security;

namespace ReneB.Portal.Tests;

public sealed class IdentityFlowTests : IClassFixture<PortalFactory>
{
    private readonly PortalFactory _factory;
    public IdentityFlowTests(PortalFactory factory) => _factory = factory;

    [Fact]
    public async Task BusinessDomainVerificationCreatesImmediatePortalAccessAndRejectsReplay()
    {
        var email = $"recruiter-{Guid.NewGuid():N}@executivesearch.example";
        using var client = CreateClient();
        var registration = await RegisterAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Contains("Check your email", await registration.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        Assert.Equal(email, outbox.Recipient);
        await database.MarkMailSentAsync(outbox.Id);
        var magicLinkValue = Regex.Match(outbox.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(magicLinkValue);

        var completion = await GetWithCsrfAsync(client, "/auth/complete");
        var response = await client.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", completion.Token), ("Token", magicLinkValue)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/portal", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/portal")).StatusCode);

        var account = await database.FindRecruiterByEmailAsync(email);
        Assert.Equal(RecruiterStatus.Active, account?.Status);

        using var replayClient = CreateClient();
        var replayPage = await GetWithCsrfAsync(replayClient, "/auth/complete");
        var replay = await replayClient.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", replayPage.Token), ("Token", magicLinkValue)));
        Assert.Contains("expired or has already been used", await replay.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FreeDomainIsVerifiedButRequiresApproval()
    {
        var email = $"recruiter-{Guid.NewGuid():N}@gmail.com";
        using var client = CreateClient();
        await RegisterAsync(client, email);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        await database.MarkMailSentAsync(outbox.Id);
        var magicLinkValue = Regex.Match(outbox.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var completion = await GetWithCsrfAsync(client, "/auth/complete");
        var response = await client.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", completion.Token), ("Token", magicLinkValue)));
        Assert.Equal("/auth/pending", response.Headers.Location?.OriginalString);
        Assert.Equal(RecruiterStatus.PendingApproval, (await database.FindRecruiterByEmailAsync(email))?.Status);
    }

    [Fact]
    public async Task DisposableDomainGetsGenericResponseWithoutStoredAccount()
    {
        var email = $"discard-{Guid.NewGuid():N}@mailinator.com";
        using var client = CreateClient();
        var response = await RegisterAsync(client, email);
        Assert.Contains("Check your email", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Null(await _factory.Services.GetRequiredService<PortalDatabase>().FindRecruiterByEmailAsync(email));
    }

    [Fact]
    public async Task SuspendingRecruiterRevokesExistingServerSession()
    {
        var email = $"recruiter-{Guid.NewGuid():N}@executivesearch.example";
        using var client = CreateClient();
        await RegisterAsync(client, email);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        await database.MarkMailSentAsync(outbox.Id);
        var magicLinkValue = Regex.Match(outbox.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var completion = await GetWithCsrfAsync(client, "/auth/complete");
        await client.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", completion.Token), ("Token", magicLinkValue)));
        var account = await database.FindRecruiterByEmailAsync(email);
        Assert.NotNull(account);

        await database.SetRecruiterStatusAsync(account.Id, RecruiterStatus.Suspended, Guid.NewGuid());

        var denied = await client.GetAsync("/portal");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Contains("/auth/login", denied.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginChallengeCanOnlyBeConsumedOnceUnderConcurrency()
    {
        var email = $"recruiter-{Guid.NewGuid():N}@executivesearch.example";
        using var client = CreateClient();
        await RegisterAsync(client, email);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var identity = _factory.Services.GetRequiredService<ReneB.Portal.Security.IdentityService>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        Assert.Contains("eight-character email verification code", outbox.Body, StringComparison.Ordinal);
        Assert.Contains("separate six-digit number from their authenticator app", outbox.Body, StringComparison.Ordinal);
        var magicLinkValue = Regex.Match(outbox.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;

        var results = await Task.WhenAll(identity.CompleteTokenAsync(magicLinkValue, default), identity.CompleteTokenAsync(magicLinkValue, default));

        Assert.Single(results, result => result is not null);
    }

    [Fact]
    public async Task ManualCodeAttemptsAreLimitedPersistentlyByEmailIdentity()
    {
        var email = $"manual-{Guid.NewGuid():N}@executivesearch.example";
        using var client = CreateClient();
        await RegisterAsync(client, email);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var identity = _factory.Services.GetRequiredService<IdentityService>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        var correctCode = Regex.Match(outbox.Body, "<strong>([A-Z0-9]{8})</strong>", RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(correctCode);
        var wrongCode = correctCode == "AAAAAAAA" ? "BBBBBBBB" : "AAAAAAAA";

        for (var attempt = 0; attempt < 8; attempt++)
        {
            Assert.Null(await identity.CompleteCodeAsync(email, wrongCode, default));
        }
        Assert.Null(await identity.CompleteCodeAsync(email, correctCode, default));

        _factory.Time.Advance(TimeSpan.FromMinutes(16));
        await identity.RequestLoginAsync(email, adminOnly: false, default);
        var replacement = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(replacement);
        var replacementCode = Regex.Match(replacement.Body, "<strong>([A-Z0-9]{8})</strong>", RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(replacementCode);
        Assert.NotNull(await identity.CompleteCodeAsync(email, replacementCode, default));
    }

    [Fact]
    public async Task RepeatRegistrationCannotRewriteOrDemoteAnExistingAccount()
    {
        var email = $"existing-{Guid.NewGuid():N}@executivesearch.example";
        using var client = CreateClient();
        await RegisterAsync(client, email);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var original = await database.FindRecruiterByEmailAsync(email);
        Assert.NotNull(original);
        await database.SetRecruiterStatusAsync(original.Id, RecruiterStatus.Active, Guid.NewGuid());

        var page = await GetWithCsrfAsync(client, "/auth/register");
        await client.PostAsync("/auth/register", Form(
            ("__RequestVerificationToken", page.Token),
            ("Input.Name", "Injected Name"),
            ("Input.Email", email),
            ("Input.Organisation", "Injected Organisation"),
            ("Input.Title", "Injected Title"),
            ("Input.ProfileUrl", "https://attacker.example"),
            ("Input.Country", "Elsewhere"),
            ("Input.Phone", string.Empty),
            ("Input.Purpose", "Attempt to overwrite an existing recruiter account with unverified data."),
            ("Input.PrivacyAccepted", "true")));

        var preserved = await database.FindRecruiterByEmailAsync(email);
        Assert.NotNull(preserved);
        Assert.Equal(original.Id, preserved.Id);
        Assert.Equal(original.Name, preserved.Name);
        Assert.Equal(original.Organisation, preserved.Organisation);
        Assert.Equal(RecruiterStatus.Active, preserved.Status);
    }

    [Fact]
    public async Task UnlistedNonDisposableDomainReceivesAccessAfterMailboxVerification()
    {
        var email = $"unknown-{Guid.NewGuid():N}@unlisted-search.example";
        using var client = CreateClient();
        await RegisterAsync(client, email);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        var magicLinkValue = Regex.Match(outbox.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var completion = await GetWithCsrfAsync(client, "/auth/complete");
        var response = await client.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", completion.Token), ("Token", magicLinkValue)));

        Assert.Equal("/portal", response.Headers.Location?.OriginalString);
        Assert.Equal(RecruiterStatus.Active, (await database.FindRecruiterByEmailAsync(email))?.Status);
    }

    [Fact]
    public async Task AdminTotpIsPersistedOnlyAfterTheSetupCodeIsVerified()
    {
        const string email = "admin@example.invalid";
        using var client = CreateClient();
        var login = await GetWithCsrfAsync(client, "/auth/admin");
        var loginResponse = await client.PostAsync("/auth/admin", Form(("__RequestVerificationToken", login.Token), ("Email", email)));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var outbox = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(outbox);
        var magicLinkValue = Regex.Match(outbox.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var completion = await GetWithCsrfAsync(client, "/auth/complete");
        var completionResponse = await client.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", completion.Token), ("Token", magicLinkValue)));
        Assert.Equal("/admin/totp", completionResponse.Headers.Location?.OriginalString);
        var account = await database.FindRecruiterByEmailAsync(email);
        Assert.NotNull(account);

        var privatePortalBeforeTotp = await client.GetAsync("/portal");
        Assert.Equal(HttpStatusCode.Redirect, privatePortalBeforeTotp.StatusCode);
        Assert.Contains("/auth/access-denied", privatePortalBeforeTotp.Headers.Location?.OriginalString, StringComparison.Ordinal);

        var setupResponse = await client.GetAsync("/admin/totp");
        Assert.True(setupResponse.StatusCode == HttpStatusCode.OK,
            $"TOTP setup returned {(int)setupResponse.StatusCode} with location {setupResponse.Headers.Location?.OriginalString}.");
        var setupHtml = await setupResponse.Content.ReadAsStringAsync();
        var setup = (Csrf: WebUtility.HtmlDecode(Regex.Match(setupHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value), Html: setupHtml);
        Assert.NotEmpty(setup.Csrf);
        Assert.Contains("Email sign-in complete", setup.Html, StringComparison.Ordinal);
        Assert.Contains("The eight-character email code will not work here", setup.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("regular expression", setup.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await database.GetAdminTotpSecretAsync(account.Id));
        var enrollment = WebUtility.HtmlDecode(Regex.Match(setup.Html, "name=\"Enrollment\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
        Assert.NotEmpty(enrollment);
        var protector = _factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(TotpModel.EnrollmentProtectionPurpose);
        var setupMaterial = Convert.FromBase64String(protector.Unprotect(enrollment));

        var invalidResponse = await client.PostAsync("/admin/totp", Form(
            ("__RequestVerificationToken", setup.Csrf), ("Enrollment", enrollment), ("ReturnUrl", string.Empty), ("Code", "ABCDEF")));
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        var invalidHtml = await invalidResponse.Content.ReadAsStringAsync();
        Assert.Contains("Enter the six-digit number from your authenticator app. The email code does not work on this step.", invalidHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("regular expression", invalidHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await database.GetAdminTotpSecretAsync(account.Id));

        var validCsrf = WebUtility.HtmlDecode(Regex.Match(invalidHtml, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
        enrollment = WebUtility.HtmlDecode(Regex.Match(invalidHtml, "name=\"Enrollment\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value);
        Assert.NotEmpty(validCsrf);
        Assert.NotEmpty(enrollment);
        var code = TotpService.GenerateCode(setupMaterial, _factory.Time.GetUtcNow());
        var response = await client.PostAsync("/admin/totp", Form(
            ("__RequestVerificationToken", validCsrf), ("Enrollment", enrollment), ("ReturnUrl", string.Empty), ("Code", code)));

        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
        Assert.Equal(setupMaterial, await database.GetAdminTotpSecretAsync(account.Id));

        var portalPage = await GetWithCsrfAsync(client, "/portal");
        Assert.DoesNotContain("Delete this recruiter account", portalPage.Html, StringComparison.Ordinal);
        var rejectedDeletion = await client.PostAsync("/portal?handler=DeleteAccount", Form(
            ("__RequestVerificationToken", portalPage.Token), ("ConfirmDeletion", "true")));
        Assert.Equal(HttpStatusCode.Forbidden, rejectedDeletion.StatusCode);

        var pendingRegistration = new RecruiterRegistration("Pending Grant", $"pending-grant-{Guid.NewGuid():N}@gmail.com",
            "Pending Search", "Recruiter", "https://example.net", "Australia", null, "A pending account must not receive a resume grant.");
        var pending = await database.UpsertPendingRecruiterAsync(pendingRegistration, DomainRisk.Free);
        var recruitersPage = await GetWithCsrfAsync(client, "/admin/recruiters");
        var pendingRow = Regex.Match(recruitersPage.Html,
            $"<tr>(?:(?!</tr>).)*{Regex.Escape(pending.Email)}(?:(?!</tr>).)*</tr>", RegexOptions.Singleline | RegexOptions.CultureInvariant).Value;
        Assert.NotEmpty(pendingRow);
        Assert.DoesNotContain("Grant résumé", pendingRow, StringComparison.Ordinal);

        var rejectedGrant = await client.PostAsync("/admin/recruiters?handler=Grant", Form(
            ("__RequestVerificationToken", recruitersPage.Token), ("id", pending.Id.ToString())));
        Assert.Equal(HttpStatusCode.Redirect, rejectedGrant.StatusCode);
    }

    [Fact]
    public async Task ExistingSuspendedRecruiterIsPromotedWhenAllowlistedAsAdministrator()
    {
        var email = $"promoted-admin-{Guid.NewGuid():N}@example.invalid";
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var existing = await database.UpsertPendingRecruiterAsync(
            new RecruiterRegistration("Existing Account", email, "Existing Search", "Partner", "https://example.net", "Australia", null,
                "Existing recruiter account that later becomes the configured administrator."), DomainRisk.Free);
        await database.SetRecruiterStatusAsync(existing.Id, RecruiterStatus.Suspended, Guid.NewGuid());

        var promoted = await database.EnsureAdminAccountAsync(email);

        Assert.NotNull(promoted);
        Assert.Equal(existing.Id, promoted.Id);
        Assert.Equal(RecruiterStatus.PendingEmail, promoted.Status);
        Assert.DoesNotContain(await database.ListRecruitersAsync(), recruiter => recruiter.Id == existing.Id);
    }

    [Fact]
    public async Task DisabledLaunchModeRejectsPreIssuedRecruiterTokenAndManualCode()
    {
        await using var disabledFactory = new PortalFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Portal:Enabled"] = "false" })));
        using var client = disabledFactory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var database = disabledFactory.Services.GetRequiredService<PortalDatabase>();
        var identity = disabledFactory.Services.GetRequiredService<IdentityService>();
        var email = $"disabled-{Guid.NewGuid():N}@executivesearch.example";
        await database.UpsertPendingRecruiterAsync(
            new RecruiterRegistration("Disabled Recruiter", email, "Search Firm", "Partner", "https://search.example", "Australia", null,
                "A challenge issued before the recruiter portal was disabled."), DomainRisk.Business);

        await identity.RequestLoginAsync(email, adminOnly: false, default);
        var tokenMail = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(tokenMail);
        var magicLinkValue = Regex.Match(tokenMail.Body, "#token=([^\"<]+)", RegexOptions.CultureInvariant).Groups[1].Value;
        var completion = await GetWithCsrfAsync(client, "/auth/complete");
        var tokenResponse = await client.PostAsync("/auth/complete?handler=Token", Form(("__RequestVerificationToken", completion.Token), ("Token", magicLinkValue)));
        Assert.Contains("expired or has already been used", await tokenResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(RecruiterStatus.PendingEmail, (await database.FindRecruiterByEmailAsync(email))?.Status);

        await identity.RequestLoginAsync(email, adminOnly: false, default);
        var codeMail = await database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(codeMail);
        var code = Regex.Match(codeMail.Body, "<strong>([A-Z0-9]{8})</strong>", RegexOptions.CultureInvariant).Groups[1].Value;
        completion = await GetWithCsrfAsync(client, "/auth/complete");
        var codeResponse = await client.PostAsync("/auth/complete?handler=Code", Form(
            ("__RequestVerificationToken", completion.Token), ("Email", email), ("Code", code)));
        Assert.Contains("expired or has already been used", await codeResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(RecruiterStatus.PendingEmail, (await database.FindRecruiterByEmailAsync(email))?.Status);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true
    });

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
    {
        var page = await GetWithCsrfAsync(client, "/auth/register");
        return await client.PostAsync("/auth/register", Form(
            ("__RequestVerificationToken", page.Token),
            ("Input.Name", "Test Recruiter"),
            ("Input.Email", email),
            ("Input.Organisation", "Executive Search Example"),
            ("Input.Title", "Search Partner"),
            ("Input.ProfileUrl", "https://search.example"),
            ("Input.Country", "Australia"),
            ("Input.Phone", string.Empty),
            ("Input.Purpose", "Sourcing a senior enterprise architecture mandate with genuine design authority."),
            ("Input.PrivacyAccepted", "true")));
    }

    private static async Task<(string Token, string Html)> GetWithCsrfAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var csrfValue = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"", RegexOptions.CultureInvariant).Groups[1].Value;
        Assert.NotEmpty(csrfValue);
        return (WebUtility.HtmlDecode(csrfValue), html);
    }

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] values)
        => new(values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value)));
}

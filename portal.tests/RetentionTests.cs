using Microsoft.Extensions.DependencyInjection;
using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Tests;

public sealed class RetentionTests : IClassFixture<PortalFactory>
{
    private readonly PortalFactory _factory;
    private readonly PortalDatabase _database;

    public RetentionTests(PortalFactory factory)
    {
        _factory = factory;
        _database = factory.Services.GetRequiredService<PortalDatabase>();
    }

    [Fact]
    public async Task InactiveAccountIsWarnedAndThenContentIsDeleted()
    {
        var warnedEmail = $"warn-{Guid.NewGuid():N}@search.example";
        var warned = await CreateAsync(warnedEmail);
        _factory.Time.Advance(TimeSpan.FromDays(151));
        await _database.RunRetentionAsync();
        var warning = await _database.FindPendingMailForRecipientAsync(warnedEmail, "retention-warning");
        Assert.NotNull(warning);
        Assert.Equal("retention-warning", warning.Kind);

        await _database.MarkMailSentAsync(warning.Id);
        await _database.TouchRecruiterAsync(warned.Id);
        _factory.Time.Advance(TimeSpan.FromDays(151));
        await _database.RunRetentionAsync();
        Assert.NotNull(await _database.FindPendingMailForRecipientAsync(warnedEmail, "retention-warning"));

        var expiredEmail = $"expire-{Guid.NewGuid():N}@search.example";
        await CreateAsync(expiredEmail);
        _factory.Time.Advance(TimeSpan.FromDays(181));
        await _database.RunRetentionAsync();
        Assert.Null(await _database.FindRecruiterByEmailAsync(expiredEmail));
    }

    [Fact]
    public async Task AccountDeletionRemovesMailPayloadsAddressedToRecruiter()
    {
        var email = $"delete-{Guid.NewGuid():N}@search.example";
        var recruiter = await CreateAsync(email);
        await _database.EnqueueMailAsync("magic-link", email, "Private access", "Sensitive one-time mail payload");
        var pending = await _database.FindPendingMailForRecipientAsync(email, "magic-link");
        Assert.NotNull(pending);
        await _database.CaptureDevelopmentMailAsync(pending);

        await _database.DeleteRecruiterAsync(recruiter.Id, recruiter.Id);

        Assert.Null(await _database.FindPendingMailForRecipientAsync(email));
        Assert.DoesNotContain(await _database.ListDevelopmentMailAsync(), item => item.Recipient == email);
        Assert.True(await _database.RecruiterRowExistsAsync(recruiter.Id));

        _factory.Time.Advance(TimeSpan.FromDays(366));
        await _database.RunRetentionAsync();
        Assert.False(await _database.RecruiterRowExistsAsync(recruiter.Id));
    }

    [Fact]
    public async Task DeletionRemovesResidualAdministratorAuthenticationSecretsImmediately()
    {
        var recruiter = await CreateAsync($"former-admin-{Guid.NewGuid():N}@search.example");
        await _database.SaveAdminTotpSecretAsync(recruiter.Id, Enumerable.Range(1, 20).Select(value => (byte)value).ToArray());
        Assert.True(await _database.TryBeginAdminTotpAttemptAsync(recruiter.Id));

        await _database.DeleteRecruiterAsync(recruiter.Id, recruiter.Id);

        Assert.Null(await _database.GetAdminTotpSecretAsync(recruiter.Id));
        Assert.False(await _database.HasAdminTotpAttemptStateAsync(recruiter.Id));
    }

    [Fact]
    public async Task ResumeRetentionAlwaysPreservesCurrentAndNewestPreviousVersion()
    {
        var actor = Guid.NewGuid();
        var first = await _database.SaveResumeAsync("first.pdf", "%PDF-1.7\nfirst\n%%EOF"u8.ToArray(), actor);
        _factory.Time.Advance(TimeSpan.FromDays(31));
        var second = await _database.SaveResumeAsync("second.pdf", "%PDF-1.7\nsecond\n%%EOF"u8.ToArray(), actor);
        _factory.Time.Advance(TimeSpan.FromDays(31));
        await _database.RunRetentionAsync();
        Assert.Equal(2, (await _database.GetResumeVersionIdsAsync()).Count);

        var third = await _database.SaveResumeAsync("third.pdf", "%PDF-1.7\nthird\n%%EOF"u8.ToArray(), actor);
        Assert.Equal(3, (await _database.GetResumeVersionIdsAsync()).Count);
        _factory.Time.Advance(TimeSpan.FromDays(31));
        await _database.RunRetentionAsync();

        var retained = await _database.GetResumeVersionIdsAsync();
        Assert.Equal(2, retained.Count);
        Assert.Contains(second.Id, retained);
        Assert.Contains(third.Id, retained);
        Assert.DoesNotContain(first.Id, retained);
    }

    private async Task<RecruiterRecord> CreateAsync(string email) => await _database.UpsertPendingRecruiterAsync(
        new RecruiterRegistration("Retention Recruiter", email, "Search Example", "Partner", "https://search.example", "Australia", null,
            "Testing a real executive architecture sourcing mandate."),
        DomainRisk.Business);
}

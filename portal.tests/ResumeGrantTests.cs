using Microsoft.Extensions.DependencyInjection;
using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Tests;

public sealed class ResumeGrantTests : IClassFixture<PortalFactory>
{
    private readonly PortalDatabase _database;
    private readonly AdjustableTimeProvider _time;
    public ResumeGrantTests(PortalFactory factory)
    {
        _database = factory.Services.GetRequiredService<PortalDatabase>();
        _time = factory.Time;
    }

    [Fact]
    public async Task GrantIsVersionBoundRevocableAndApproximatelyThirtyDays()
    {
        var actor = Guid.NewGuid();
        var content = "%PDF-1.7\nvalidated fixture\n%%EOF"u8.ToArray();
        var resume = await _database.SaveResumeAsync("rene-resume.pdf", content, actor);
        var registration = new RecruiterRegistration("Grant Test", $"grant-{Guid.NewGuid():N}@search.example", "Search", "Partner", "https://search.example", "Australia", null, "Senior architecture search mandate");
        var recruiter = await _database.UpsertPendingRecruiterAsync(registration, DomainRisk.Business);
        await _database.SetRecruiterStatusAsync(recruiter.Id, RecruiterStatus.Active, actor);

        await _database.GrantResumeAsync(recruiter.Id, actor);
        var grant = await _database.GetResumeGrantAsync(recruiter.Id);
        Assert.NotNull(grant);
        Assert.Equal(resume.Id, grant.ResumeId);
        Assert.InRange(grant.ExpiresAt - grant.GrantedAt, TimeSpan.FromDays(29.99), TimeSpan.FromDays(30.01));
        Assert.Equal(content, (await _database.GetResumeForRecruiterAsync(recruiter.Id))?.Content);

        await _database.RecordResumeDownloadAsync(recruiter.Id, resume.Id);
        Assert.True(await _database.HasAuditEventAsync("resume.downloaded", recruiter.Id, resume.Id));

        await _database.RevokeResumeAsync(recruiter.Id, actor);
        Assert.Null(await _database.GetResumeForRecruiterAsync(recruiter.Id));
    }

    [Fact]
    public async Task PendingRecruiterCannotReceiveResumeGrant()
    {
        var registration = new RecruiterRegistration("Pending Test", $"pending-{Guid.NewGuid():N}@gmail.com", "Independent", "Recruiter", "https://example.net", "Australia", null, "Senior architecture search mandate");
        var recruiter = await _database.UpsertPendingRecruiterAsync(registration, DomainRisk.Free);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.GrantResumeAsync(recruiter.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ResumeRequestsArePersistentlyDeduplicatedForTwentyFourHours()
    {
        var registration = new RecruiterRegistration("Request Test", $"request-{Guid.NewGuid():N}@search.example", "Search", "Partner",
            "https://search.example", "Australia", null, "Requesting resume access for a relevant senior architecture mandate.");
        var recruiter = await _database.UpsertPendingRecruiterAsync(registration, DomainRisk.Business);

        Assert.True(await _database.TryQueueResumeAccessRequestAsync(recruiter.Id, null));
        Assert.False(await _database.TryQueueResumeAccessRequestAsync(recruiter.Id, null));
        _time.Advance(TimeSpan.FromHours(25));
        Assert.True(await _database.TryQueueResumeAccessRequestAsync(recruiter.Id, null));
    }
}

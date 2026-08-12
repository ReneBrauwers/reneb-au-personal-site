using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ReneB.Portal.Data;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class MailDeliveryTests : IClassFixture<PortalFactory>
{
    private readonly PortalDatabase _database;

    public MailDeliveryTests(PortalFactory factory) => _database = factory.Services.GetRequiredService<PortalDatabase>();

    [Fact]
    public async Task TransportTimeoutAdvancesOutboxRetryState()
    {
        var recipient = $"timeout-{Guid.NewGuid():N}@example.invalid";
        await _database.EnqueueMailAsync("timeout-test", recipient, "Timeout test", "No private body content");
        var services = new ServiceCollection()
            .AddSingleton(_database)
            .AddSingleton<IMailTransport, TimeoutMailTransport>()
            .BuildServiceProvider();

        await OutboxWorker.ProcessDueAsync(services, NullLogger<OutboxWorker>.Instance, CancellationToken.None);

        var pending = await _database.FindPendingMailForRecipientAsync(recipient, "timeout-test");
        Assert.NotNull(pending);
        Assert.Equal(1, pending.AttemptCount);
    }

    private sealed class TimeoutMailTransport : IMailTransport
    {
        public Task SendAsync(PortalDatabase.OutboxRecord message, CancellationToken cancellationToken)
            => throw new TaskCanceledException("Synthetic transport timeout.");
    }
}

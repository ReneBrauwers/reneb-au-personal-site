using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Identity.Client;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;

namespace ReneB.Portal.Services;

public static class MailConfigurationValidator
{
    public static bool IsReady(MailOptions options)
    {
        if (string.Equals(options.Mode, "Development", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (!string.Equals(options.Mode, "Graph", StringComparison.OrdinalIgnoreCase)
            || !Guid.TryParse(options.TenantId, out _)
            || !Guid.TryParse(options.ClientId, out _)
            || !MailAddress.TryCreate(options.SenderMailbox, out _)
            || !File.Exists(options.CertificatePath)
            || !File.Exists(options.PrivateKeyPath))
        {
            return false;
        }
        try
        {
            using var certificate = X509Certificate2.CreateFromPemFile(options.CertificatePath, options.PrivateKeyPath);
            return certificate.HasPrivateKey;
        }
        catch (Exception exception) when (exception is CryptographicException or IOException)
        {
            return false;
        }
    }
}

public interface IMailTransport
{
    Task SendAsync(PortalDatabase.OutboxRecord message, CancellationToken cancellationToken);
}

public sealed class DevelopmentMailTransport(PortalDatabase database) : IMailTransport
{
    public Task SendAsync(PortalDatabase.OutboxRecord message, CancellationToken cancellationToken)
        => database.CaptureDevelopmentMailAsync(message, cancellationToken);
}

public sealed class GraphMailTransport : IMailTransport
{
    private readonly HttpClient _httpClient;
    private readonly MailOptions _options;
    private readonly IConfidentialClientApplication _client;

    public GraphMailTransport(HttpClient httpClient, IOptions<MailOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        var certificate = X509Certificate2.CreateFromPemFile(_options.CertificatePath, _options.PrivateKeyPath);
        _client = ConfidentialClientApplicationBuilder.Create(_options.ClientId)
            .WithTenantId(_options.TenantId)
            .WithCertificate(certificate)
            .Build();
    }

    public async Task SendAsync(PortalDatabase.OutboxRecord message, CancellationToken cancellationToken)
    {
        var token = await _client.AcquireTokenForClient(["https://graph.microsoft.com/.default"])
            .ExecuteAsync(cancellationToken);
        var payload = JsonSerializer.Serialize(new
        {
            message = new
            {
                subject = message.Subject,
                body = new { contentType = "HTML", content = message.Body },
                toRecipients = new[] { new { emailAddress = new { address = message.Recipient } } }
            },
            saveToSentItems = true
        });
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(_options.SenderMailbox)}/sendMail")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await ProcessDueAsync(scope.ServiceProvider, logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Outbox processing failed without exposing message content.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal static async Task ProcessDueAsync(IServiceProvider services, ILogger<OutboxWorker> logger, CancellationToken cancellationToken)
    {
        var database = services.GetRequiredService<PortalDatabase>();
        var message = await database.GetDueMailAsync(cancellationToken);
        if (message is null)
        {
            return;
        }
        try
        {
            var transport = services.GetRequiredService<IMailTransport>();
            await transport.SendAsync(message, cancellationToken);
            await database.MarkMailSentAsync(message.Id, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning("Mail delivery failed with {ErrorType}; the encrypted outbox item will be retried.", exception.GetType().Name);
            await database.MarkMailFailedAsync(message.Id, message.AttemptCount, exception.GetType().Name, cancellationToken);
        }
    }
}

using System.Net;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class AiAuthoringTests
{
    [Fact]
    public async Task ProviderCredentialIsNotStoredAsPlaintextAndCanBeDeleted()
    {
        await using var factory = new PortalFactory();
        var database = factory.Services.GetRequiredService<PortalDatabase>();
        var options = factory.Services.GetRequiredService<IOptions<PortalOptions>>().Value;
        var apiKey = new string('k', 32);
        await database.SaveAiProviderKeyAsync(AiProviderKind.OpenRouter, apiKey, Guid.NewGuid());

        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(options.DataDirectory, "recruiter-portal.sqlite3") }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ApiKeyEncrypted FROM AiProviderConfigurations WHERE Provider='OpenRouter';";
        var stored = Assert.IsType<string>(await command.ExecuteScalarAsync());
        Assert.DoesNotContain(apiKey, stored, StringComparison.Ordinal);
        Assert.Equal(apiKey, (await database.GetAiProviderSecretAsync(AiProviderKind.OpenRouter))!.ApiKey);

        await database.DeleteAiProviderAsync(AiProviderKind.OpenRouter, Guid.NewGuid());
        Assert.Null(await database.GetAiProviderSecretAsync(AiProviderKind.OpenRouter));
    }

    [Fact]
    public async Task ReservationsEnforceProviderAndSiteWideMonthlyCeilings()
    {
        await using var factory = new PortalFactory();
        var database = factory.Services.GetRequiredService<PortalDatabase>();
        var actor = Guid.NewGuid();
        await database.SaveAiProviderKeyAsync(AiProviderKind.OpenRouter, new string('b', 32), actor);
        await database.SaveAiProviderSettingsAsync(AiProviderKind.OpenRouter, "provider/model", 1m, 1000, actor);
        await database.SaveAiGlobalBudgetAsync(.5m, actor);

        var first = await database.ReserveAiBudgetAsync(AiProviderKind.OpenRouter, .4m);
        Assert.NotNull(first);
        Assert.Null(await database.ReserveAiBudgetAsync(AiProviderKind.OpenRouter, .11m));
        await database.CompleteAiUsageAsync(first!.Value, .3m, 100, 50, "Completed");
        Assert.NotNull(await database.ReserveAiBudgetAsync(AiProviderKind.OpenRouter, .2m));
        Assert.Null(await database.ReserveAiBudgetAsync(AiProviderKind.OpenRouter, .000001m));
    }

    [Fact]
    public async Task OpenRouterDiscoveryFiltersCapabilitiesAndRequestsPrivateStructuredOutput()
    {
        var handler = new RecordingHandler(request => request.Method == HttpMethod.Get
            ? Json("""{"data":[{"id":"good/model","name":"Good","context_length":64000,"architecture":{"input_modalities":["text"],"output_modalities":["text"]},"supported_parameters":["structured_outputs"],"pricing":{"prompt":"0.000001","completion":"0.000002"}},{"id":"bad/model","name":"Bad","context_length":8000,"architecture":{"input_modalities":["text"],"output_modalities":["text"]},"supported_parameters":[],"pricing":{"prompt":"0","completion":"0"}}]}""")
            : Json("""{"id":"req-1","choices":[{"message":{"content":"{\"summary\":\"ok\",\"contentJson\":\"{}\"}"}}],"usage":{"prompt_tokens":10,"completion_tokens":5,"cost":0.01}}"""));
        var provider = new OpenRouterAuthoringProvider(new FakeHttpClientFactory(handler), Options.Create(new AiOptions()));
        var models = await provider.DiscoverModelsAsync(new string('d', 32), default);
        Assert.Single(models);
        Assert.Equal("good/model", models[0].Id);

        await provider.ProposeAsync(new string('p', 32), "good/model", RequestData(), default);
        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.True(body.RootElement.GetProperty("provider").GetProperty("require_parameters").GetBoolean());
        Assert.Equal("deny", body.RootElement.GetProperty("provider").GetProperty("data_collection").GetString());
        Assert.Equal("json_schema", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.DoesNotContain("tools", handler.LastBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task XaiRequestIsStatelessAndRecordsObservedRetentionAndExactCost()
    {
        var response = Json("""{"id":"resp-1","output":[{"type":"message","content":[{"type":"output_text","text":"{\"summary\":\"ok\",\"contentJson\":\"{}\"}"}]}],"usage":{"input_tokens":20,"output_tokens":10,"cost_in_usd_ticks":25000000}}""");
        response.Headers.TryAddWithoutValidation("x-zero-data-retention", "true");
        var handler = new RecordingHandler(_ => response);
        var provider = new XaiAuthoringProvider(new FakeHttpClientFactory(handler), Options.Create(new AiOptions()));
        var result = await provider.ProposeAsync(new string('x', 32), "grok-test", RequestData(), default);

        using var body = JsonDocument.Parse(handler.LastBody!);
        Assert.False(body.RootElement.GetProperty("store").GetBoolean());
        Assert.Equal("json_schema", body.RootElement.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
        Assert.True(result.ZeroDataRetentionObserved);
        Assert.Equal(0.0025m, result.CostUsd);
    }

    [Fact]
    public async Task ContextExtractorAcceptsSafeDocxAndRejectsActiveOrInvalidContent()
    {
        var extractor = new AiContextExtractor(new PdfValidator(), Options.Create(new AiOptions()));
        var safe = FormFile("context.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Docx(("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p><w:r><w:t>Useful evidence</w:t></w:r></w:p></w:body></w:document>")));
        var accepted = await extractor.ExtractAsync(safe, default);
        Assert.True(accepted.Valid, accepted.Error);
        Assert.Equal("Useful evidence", accepted.ExtractedText);

        var active = FormFile("active.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Docx(("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>"), ("word/vbaProject.bin", "macro")));
        var rejected = await extractor.ExtractAsync(active, default);
        Assert.False(rejected.Valid);
        Assert.Contains("active-content", rejected.Error, StringComparison.OrdinalIgnoreCase);

        var invalidUtf8 = FormFile("bad.txt", "text/plain", [0xff, 0xfe, 0xfd]);
        Assert.False((await extractor.ExtractAsync(invalidUtf8, default)).Valid);
    }

    private static AiAuthoringRequest RequestData() => new()
    {
        SystemInstructions = "system",
        UserRequest = "request",
        CurrentContentJson = "{}",
        ContentSchemaDescription = "schema",
        Context = [],
        MaximumOutputTokens = 128
    };

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static IFormFile FormFile(string name, string contentType, byte[] content)
    {
        var file = new FormFile(new MemoryStream(content, writable: false), 0, content.Length, "upload", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        return file;
    }

    private static byte[] Docx(params (string Name, string Content)[] entries)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        return memory.ToArray();
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public string? LastBody { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return response(request);
        }
    }
}

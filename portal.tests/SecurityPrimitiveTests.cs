using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text.Json;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Models;
using ReneB.Portal.Security;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class SecurityPrimitiveTests : IClassFixture<PortalFactory>
{
    private readonly PortalFactory _factory;
    public SecurityPrimitiveTests(PortalFactory factory) => _factory = factory;

    [Fact]
    public void FieldEncryptionRoundTripsWithoutPlaintext()
    {
        var encryption = _factory.Services.GetRequiredService<FieldEncryptionService>();
        const string plaintext = "Sensitive recruiter message and compensation terms";
        var encrypted = encryption.Encrypt(plaintext);
        Assert.DoesNotContain("Sensitive", encrypted, StringComparison.Ordinal);
        Assert.Equal(plaintext, encryption.Decrypt(encrypted));
        Assert.Equal(encryption.LookupHash(" Person@Example.COM "), encryption.LookupHash("person@example.com"));
    }

    [Theory]
    [InlineData("person@executivesearch.example", DomainRisk.Business)]
    [InlineData("person@gmail.com", DomainRisk.Free)]
    [InlineData("person@sub.gmail.com", DomainRisk.Free)]
    [InlineData("person@mailinator.com", DomainRisk.Disposable)]
    [InlineData("person@sub.mailinator.com", DomainRisk.Disposable)]
    [InlineData("person@gmail.com.", DomainRisk.Free)]
    public void DomainRiskIsDeterministic(string email, DomainRisk expected)
        => Assert.Equal(expected, DomainRiskClassifier.Classify(email, ["gmail.com"], ["mailinator.com"]));

    [Fact]
    public void ExplicitlyUntrustedDomainsRequireApprovalWhileOtherDomainsAreBusiness()
    {
        Assert.Equal(DomainRisk.Free, DomainRiskClassifier.Classify("person@outlook.com", ["outlook.com"]));
        Assert.Equal(DomainRisk.Business, DomainRiskClassifier.Classify("person@executivesearch.example", ["outlook.com"]));
    }

    [Fact]
    public void TotpAcceptsCurrentCodeAndRejectsDifferentCode()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
        var secret = Enumerable.Range(1, 20).Select(value => (byte)value).ToArray();
        var code = TotpService.GenerateCode(secret, now);
        Assert.True(TotpService.Validate(secret, code, now));
        Assert.False(TotpService.Validate(secret, code == "000000" ? "000001" : "000000", now));
    }

    [Fact]
    public async Task TotpAttemptsAreLimitedPersistentlyPerAdministratorAccount()
    {
        var database = _factory.Services.GetRequiredService<PortalDatabase>();
        var account = await database.EnsureAdminAccountAsync($"throttle-{Guid.NewGuid():N}@example.invalid");
        Assert.NotNull(account);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            Assert.True(await database.TryBeginAdminTotpAttemptAsync(account.Id));
        }
        Assert.False(await database.TryBeginAdminTotpAttemptAsync(account.Id));

        _factory.Time.Advance(TimeSpan.FromMinutes(16));
        Assert.True(await database.TryBeginAdminTotpAttemptAsync(account.Id));
        await database.ClearAdminTotpAttemptsAsync(account.Id);
        Assert.True(await database.TryBeginAdminTotpAttemptAsync(account.Id));
    }

    [Fact]
    public void PdfJsonInspectionRejectsExactActiveObjectsWithoutBinarySubstringFalsePositives()
    {
        const string active = """{"encrypt":{"encrypted":false},"attachments":{},"qpdf":[{"obj:1 0 R":{"value":{"/OpenAction":"2 0 R","/Type":"/Catalog"}}}]}""";
        const string harmless = """{"encrypt":{"encrypted":false},"attachments":{},"qpdf":[{"obj:1 0 R":{"value":{"/AALabel":"compressed stream bytes mention /JS","/Type":"/Catalog"}}}]}""";

        Assert.True(PdfValidator.ContainsUnsafeFeatures(active));
        Assert.False(PdfValidator.ContainsUnsafeFeatures(harmless));
    }

    [Theory]
    [InlineData("{\"encrypt\":{\"encrypted\":true},\"attachments\":{},\"qpdf\":[]}")]
    [InlineData("{\"encrypt\":{\"encrypted\":false},\"attachments\":{\"resume.bin\":{}},\"qpdf\":[]}")]
    [InlineData("{\"encrypt\":{\"encrypted\":false},\"attachments\":{},\"qpdf\":[{\"obj\":{\"value\":{\"/S\":\"/Launch\"}}}]}")]
    [InlineData("{\"encrypt\":{\"encrypted\":false},\"attachments\":{},\"qpdf\":[{\"obj\":{\"value\":{\"/XFA\":\"2 0 R\"}}}]}")]
    [InlineData("{\"encrypt\":{\"encrypted\":false},\"attachments\":{},\"qpdf\":[{\"obj\":{\"value\":{\"/S\":\"/RichMediaExecute\"}}}]}")]
    [InlineData("{\"encrypt\":{\"encrypted\":false},\"attachments\":{},\"acroform\":{\"hasacroform\":true},\"qpdf\":[]}")]
    public void PdfJsonInspectionRejectsEncryptedAttachmentsAndLaunchActions(string structure)
        => Assert.True(PdfValidator.ContainsUnsafeFeatures(structure));

    [Fact]
    public void LookupHashesRemainStableWhenTheActiveEncryptionKeyRotates()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"reneb-keyring-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "keyring.json");
        var lookupMaterial = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var materialV1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var materialV2 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { activeKeyId = "v1", lookupKey = lookupMaterial, keys = new Dictionary<string, string> { ["v1"] = materialV1, ["v2"] = materialV2 } }));
            var environment = _factory.Services.GetRequiredService<IWebHostEnvironment>();
            var first = new FieldEncryptionService(Options.Create(new EncryptionOptions { KeyFile = path }), environment);
            var encrypted = first.Encrypt("rotation proof");
            var lookup = first.LookupHash("person@example.com");

            File.WriteAllText(path, JsonSerializer.Serialize(new { activeKeyId = "v2", lookupKey = lookupMaterial, keys = new Dictionary<string, string> { ["v1"] = materialV1, ["v2"] = materialV2 } }));
            var second = new FieldEncryptionService(Options.Create(new EncryptionOptions { KeyFile = path }), environment);

            Assert.Equal(lookup, second.LookupHash("person@example.com"));
            Assert.Equal("rotation proof", second.Decrypt(encrypted));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("resume.txt", "application/pdf", "%PDF-1.7\n%%EOF", "Only PDF")]
    [InlineData("resume.pdf", "application/octet-stream", "%PDF-1.7\n%%EOF", "Only PDF")]
    [InlineData("resume.pdf", "application/pdf", "not a pdf", "structurally recognisable")]
    public async Task PdfValidatorRejectsRenamedOrMalformedFiles(string fileName, string contentType, string content, string error)
    {
        var bytes = System.Text.Encoding.Latin1.GetBytes(content);
        await using var stream = new MemoryStream(bytes);
        var upload = new FormFile(stream, 0, bytes.Length, "upload", fileName) { Headers = new HeaderDictionary(), ContentType = contentType };
        var result = await new PdfValidator().ValidateAsync(upload, CancellationToken.None);
        Assert.False(result.Valid);
        Assert.Contains(error, result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PdfValidatorRejectsOversizedUploadBeforeReading()
    {
        await using var stream = new MemoryStream("%PDF-1.7\n%%EOF"u8.ToArray());
        var upload = new FormFile(stream, 0, PdfValidator.MaximumBytes + 1L, "upload", "resume.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        var result = await new PdfValidator().ValidateAsync(upload, CancellationToken.None);
        Assert.False(result.Valid);
        Assert.Contains("5 MB", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}

using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;

namespace ReneB.Portal.Services;

public sealed class AiContextExtractor(PdfValidator pdfValidator, IOptions<AiOptions> options)
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly AiOptions _options = options.Value;

    public async Task<(bool Valid, string Error, byte[] Content, string ExtractedText, string MediaType)> ExtractAsync(IFormFile upload, CancellationToken cancellationToken)
    {
        if (upload.Length <= 0 || upload.Length > _options.MaximumContextFileBytes) return Failure("Select a non-empty context file no larger than 10 MB.");
        var extension = Path.GetExtension(upload.FileName).ToLowerInvariant();
        if (extension == ".pdf" && upload.ContentType == "application/pdf")
        {
            var validation = await pdfValidator.ValidateAsync(upload, cancellationToken, checked((int)_options.MaximumContextFileBytes));
            if (!validation.Valid) return Failure(validation.Error);
        }
        await using var stream = upload.OpenReadStream();
        using var memory = new MemoryStream(checked((int)upload.Length)); await stream.CopyToAsync(memory, cancellationToken); var bytes = memory.ToArray();
        try
        {
            return extension switch
            {
                ".txt" when upload.ContentType is "text/plain" => Success(bytes, StrictUtf8.GetString(bytes), "text/plain"),
                ".md" when upload.ContentType is "text/markdown" or "text/plain" => Success(bytes, StrictUtf8.GetString(bytes), "text/markdown"),
                ".docx" when upload.ContentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractDocx(bytes),
                ".pdf" when upload.ContentType == "application/pdf" => await ExtractPdfAsync(bytes, cancellationToken),
                _ => Failure("Only PDF, DOCX, UTF-8 TXT and Markdown context files are accepted.")
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or DecoderFallbackException or System.Xml.XmlException)
        {
            return Failure("The context file is malformed or is not valid UTF-8 text.");
        }
    }

    public async Task<(bool Valid, string Error, byte[] Content, string ExtractedText, string MediaType)> ExtractStoredResumeAsync(string fileName, byte[] content, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(content, writable: false);
        var upload = new FormFile(stream, 0, content.LongLength, "resume", Path.GetFileName(fileName))
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        return await ExtractAsync(upload, cancellationToken);
    }

    private static async Task<(bool Valid, string Error, byte[] Content, string ExtractedText, string MediaType)> ExtractPdfAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        var temp = Path.Combine(Path.GetTempPath(), $"ai-context-{Guid.NewGuid():N}.pdf");
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
            using var process = new Process { StartInfo = new ProcessStartInfo { FileName = "pdftotext", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
            foreach (var argument in new[] { "-enc", "UTF-8", "-nopgbrk", temp, "-" }) process.StartInfo.ArgumentList.Add(argument);
            process.Start(); var output = process.StandardOutput.ReadToEndAsync(cancellationToken); var error = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken); _ = await error;
            if (process.ExitCode != 0) return Failure("Text could not be extracted safely from the PDF.");
            return Success(bytes, (await output).Trim(), "application/pdf");
        }
        catch (System.ComponentModel.Win32Exception) { return Failure("The PDF text extraction service is unavailable."); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static (bool Valid, string Error, byte[] Content, string ExtractedText, string MediaType) ExtractDocx(byte[] bytes)
    {
        using var memory = new MemoryStream(bytes, writable: false); using var archive = new ZipArchive(memory, ZipArchiveMode.Read);
        if (archive.Entries.Count is 0 or > 2000 || archive.Entries.Sum(entry => entry.Length) > 50L * 1024 * 1024) return Failure("The DOCX package is empty or expands beyond the safe limit.");
        if (archive.Entries.Any(entry => entry.FullName.Contains("..", StringComparison.Ordinal) || entry.FullName.StartsWith("/", StringComparison.Ordinal)
            || entry.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) || entry.FullName.Contains("/embeddings/", StringComparison.OrdinalIgnoreCase)
            || entry.FullName.Contains("/activeX/", StringComparison.OrdinalIgnoreCase) || entry.FullName.EndsWith(".bin", StringComparison.OrdinalIgnoreCase)))
            return Failure("Macro-enabled, embedded-object and active-content Office documents are not accepted.");
        foreach (var relationships in archive.Entries.Where(entry => entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
        {
            using var relStream = relationships.Open(); var xml = XDocument.Load(relStream, LoadOptions.None);
            if (xml.Descendants().Any(node => string.Equals((string?)node.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)))
                return Failure("Office documents containing external relationships are not accepted.");
        }
        var documentEntry = archive.GetEntry("word/document.xml"); if (documentEntry is null) return Failure("The DOCX document body is missing.");
        using var documentStream = documentEntry.Open(); var document = XDocument.Load(documentStream, LoadOptions.None);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document.Descendants(word + "p").Select(paragraph => string.Concat(paragraph.Descendants(word + "t").Select(value => value.Value))).Where(value => !string.IsNullOrWhiteSpace(value));
        return Success(bytes, string.Join(Environment.NewLine, paragraphs), "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
    }

    private static (bool, string, byte[], string, string) Success(byte[] bytes, string text, string mediaType)
        => string.IsNullOrWhiteSpace(text) ? Failure("No usable text could be extracted from the context file.") : (true, string.Empty, bytes, text.Length > 500_000 ? text[..500_000] : text, mediaType);
    private static (bool, string, byte[], string, string) Failure(string error) => (false, error, [], string.Empty, string.Empty);
}

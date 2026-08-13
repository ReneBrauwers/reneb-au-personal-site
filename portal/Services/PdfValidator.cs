using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ReneB.Portal.Services;

public sealed class PdfValidator
{
    public const int MaximumBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> ForbiddenPdfNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "/JavaScript", "/JS", "/Launch", "/EmbeddedFile", "/EmbeddedFiles", "/Filespec", "/OpenAction", "/AA",
        "/XFA", "/AcroForm", "/RichMedia", "/RichMediaContent", "/RichMediaSettings", "/RichMediaExecute",
        "/Movie", "/Sound", "/Rendition", "/Screen", "/3D", "/3DD", "/3DV", "/3DA", "/FileAttachment",
        "/SubmitForm", "/ImportData", "/GoToE"
    };

    public async Task<(bool Valid, string Error)> ValidateAsync(IFormFile upload, CancellationToken cancellationToken, int maximumBytes = MaximumBytes)
    {
        if (upload.Length <= 0 || upload.Length > maximumBytes)
        {
            return (false, $"Select a non-empty PDF no larger than {maximumBytes / 1024 / 1024} MB.");
        }
        if (!string.Equals(Path.GetExtension(upload.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(upload.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Only PDF files are accepted.");
        }

        var temp = Path.Combine(Path.GetTempPath(), $"resume-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(temp))
            {
                await upload.CopyToAsync(stream, cancellationToken);
            }
            var content = await File.ReadAllBytesAsync(temp, cancellationToken);
            if (content.Length < 8 || !content.AsSpan(0, 5).SequenceEqual("%PDF-"u8)
                || !Encoding.Latin1.GetString(content.AsSpan(Math.Max(0, content.Length - 1024))).Contains("%%EOF", StringComparison.Ordinal))
            {
                return (false, "The upload is not a structurally recognisable PDF.");
            }
            var check = await RunQpdfAsync(["--check", temp], cancellationToken);
            if (check.ExitCode != 0)
            {
                return (false, "The PDF failed structural validation.");
            }

            var structure = await RunQpdfAsync(["--json", "--json-stream-data=none", temp], cancellationToken);
            if (structure.ExitCode != 0)
            {
                return (false, "The PDF structure could not be inspected safely.");
            }
            if (ContainsUnsafeFeatures(structure.Output))
            {
                return (false, "Encrypted PDFs and PDFs containing active content, launch actions or embedded files are not accepted.");
            }
            return (true, string.Empty);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, "The PDF validation service is unavailable.");
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    internal static bool ContainsUnsafeFeatures(string qpdfJson)
    {
        using var document = JsonDocument.Parse(qpdfJson);
        var root = document.RootElement;
        if (root.TryGetProperty("encrypt", out var encryption)
            && encryption.TryGetProperty("encrypted", out var encrypted)
            && encrypted.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        if (root.TryGetProperty("attachments", out var attachments)
            && attachments.ValueKind == JsonValueKind.Object
            && attachments.EnumerateObject().Any())
        {
            return true;
        }
        if (root.TryGetProperty("acroform", out var acroform)
            && acroform.TryGetProperty("hasacroform", out var hasAcroForm)
            && hasAcroForm.ValueKind == JsonValueKind.True)
        {
            return true;
        }
        return ContainsForbiddenName(root);
    }

    private static bool ContainsForbiddenName(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (ForbiddenPdfNames.Contains(property.Name)
                    || ContainsForbiddenName(property.Value))
                {
                    return true;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(ContainsForbiddenName);
        }
        else if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString() is { } name && ForbiddenPdfNames.Contains(name);
        }
        return false;
    }

    private static async Task<(int ExitCode, string Output)> RunQpdfAsync(IEnumerable<string> arguments, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "qpdf",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var output = await standardOutput;
        _ = await standardError;
        return (process.ExitCode, output);
    }
}

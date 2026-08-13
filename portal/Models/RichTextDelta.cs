using System.Net;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ReneB.Portal.Models;

public static class RichTextDelta
{
    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "bold", "italic", "link", "header", "list"
    };

    public static string CreateParagraphs(IEnumerable<string> paragraphs)
    {
        var operations = paragraphs
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => new Dictionary<string, object> { ["insert"] = value.Trim() + "\n" })
            .ToArray();
        if (operations.Length == 0)
        {
            operations = [new Dictionary<string, object> { ["insert"] = "\n" }];
        }
        return JsonSerializer.Serialize(new Dictionary<string, object> { ["ops"] = operations });
    }

    public static string CreateParagraphs(params string[] paragraphs) => CreateParagraphs((IEnumerable<string>)paragraphs);

    public static bool TryValidate(string? deltaJson, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(deltaJson) || deltaJson.Length > 100_000)
        {
            error = "Rich text is empty or exceeds its permitted size.";
            return false;
        }

        try
        {
            var root = JsonNode.Parse(deltaJson) as JsonObject;
            if (root is null || root.Count != 1 || root["ops"] is not JsonArray operations || operations.Count == 0)
            {
                error = "Rich text must contain one non-empty operations array.";
                return false;
            }

            var characters = 0;
            foreach (var node in operations)
            {
                if (node is not JsonObject operation || operation["insert"] is not JsonValue insertValue
                    || !insertValue.TryGetValue<string>(out var insert) || operation.Count > 2)
                {
                    error = "Only text insertion operations are permitted.";
                    return false;
                }
                characters += insert.Length;
                if (characters > 20_000 || insert.IndexOf('\0') >= 0)
                {
                    error = "Rich text exceeds the permitted character limit or contains invalid characters.";
                    return false;
                }
                if (operation["attributes"] is not JsonObject attributes)
                {
                    continue;
                }
                foreach (var attribute in attributes)
                {
                    if (!AllowedAttributes.Contains(attribute.Key) || !ValidateAttribute(attribute.Key, attribute.Value, insert))
                    {
                        error = $"The rich-text attribute '{attribute.Key}' is not permitted.";
                        return false;
                    }
                }
            }

            if (!GetOperations(root).Last().Insert.EndsWith('\n'))
            {
                error = "Rich text must end with a newline.";
                return false;
            }
            return true;
        }
        catch (JsonException)
        {
            error = "Rich text is not valid JSON.";
            return false;
        }
    }

    public static string ToPlainText(RichTextContent content)
    {
        EnsureValid(content.DeltaJson);
        var root = JsonNode.Parse(content.DeltaJson)!.AsObject();
        return string.Concat(GetOperations(root).Select(operation => operation.Insert)).Trim();
    }

    public static string ToHtml(RichTextContent content)
    {
        EnsureValid(content.DeltaJson);
        var root = JsonNode.Parse(content.DeltaJson)!.AsObject();
        var output = new StringBuilder();
        var inline = new StringBuilder();
        string? openList = null;

        foreach (var operation in GetOperations(root))
        {
            var chunks = operation.Insert.Split('\n');
            for (var index = 0; index < chunks.Length; index++)
            {
                if (chunks[index].Length > 0)
                {
                    inline.Append(FormatInline(chunks[index], operation.Attributes));
                }
                if (index == chunks.Length - 1)
                {
                    continue;
                }

                var lineType = LineType(operation.Attributes);
                if (lineType is "ul" or "ol")
                {
                    if (!string.Equals(openList, lineType, StringComparison.Ordinal))
                    {
                        CloseList(output, ref openList);
                        output.Append('<').Append(lineType).Append('>');
                        openList = lineType;
                    }
                    output.Append("<li>").Append(inline.Length == 0 ? "<br>" : inline).Append("</li>");
                }
                else
                {
                    CloseList(output, ref openList);
                    var tag = lineType ?? "p";
                    output.Append('<').Append(tag).Append('>')
                        .Append(inline.Length == 0 ? "<br>" : inline)
                        .Append("</").Append(tag).Append('>');
                }
                inline.Clear();
            }
        }
        CloseList(output, ref openList);
        return output.ToString();
    }

    public static string ToMarkdown(RichTextContent content)
    {
        EnsureValid(content.DeltaJson);
        var root = JsonNode.Parse(content.DeltaJson)!.AsObject();
        var output = new StringBuilder();
        var inline = new StringBuilder();
        foreach (var operation in GetOperations(root))
        {
            var chunks = operation.Insert.Split('\n');
            for (var index = 0; index < chunks.Length; index++)
            {
                if (chunks[index].Length > 0)
                {
                    inline.Append(FormatMarkdownInline(chunks[index], operation.Attributes));
                }
                if (index == chunks.Length - 1) continue;
                var prefix = LineType(operation.Attributes) switch
                {
                    "h2" => "## ",
                    "h3" => "### ",
                    "ul" => "- ",
                    "ol" => "1. ",
                    _ => string.Empty
                };
                output.Append(prefix).AppendLine(inline.ToString()).AppendLine();
                inline.Clear();
            }
        }
        return output.ToString().Trim();
    }

    private static IEnumerable<DeltaOperation> GetOperations(JsonObject root)
    {
        foreach (var node in root["ops"]!.AsArray())
        {
            var operation = node!.AsObject();
            yield return new DeltaOperation(
                operation["insert"]!.GetValue<string>(),
                operation["attributes"] as JsonObject);
        }
    }

    private static bool ValidateAttribute(string name, JsonNode? value, string insert) => name switch
    {
        "bold" or "italic" => value is JsonValue boolean && boolean.TryGetValue<bool>(out _),
        "link" => value is JsonValue linkValue && linkValue.TryGetValue<string>(out var link) && IsSafeLink(link),
        "header" => insert.Contains('\n') && value is JsonValue headerValue && headerValue.TryGetValue<int>(out var header) && header is 2 or 3,
        "list" => insert.Contains('\n') && value is JsonValue listValue && listValue.TryGetValue<string>(out var list) && list is "bullet" or "ordered",
        _ => false
    };

    private static bool IsSafeLink(string value)
    {
        if (value.StartsWith("/", StringComparison.Ordinal) && !value.StartsWith("//", StringComparison.Ordinal)) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "mailto"
            && string.IsNullOrEmpty(uri.UserInfo);
    }

    private static string? LineType(JsonObject? attributes)
    {
        if (attributes?["header"] is JsonValue header && header.TryGetValue<int>(out var level)) return $"h{level}";
        if (attributes?["list"] is JsonValue list && list.TryGetValue<string>(out var value)) return value == "ordered" ? "ol" : "ul";
        return null;
    }

    private static string FormatInline(string value, JsonObject? attributes)
    {
        var encoded = WebUtility.HtmlEncode(value);
        if (attributes?["bold"]?.GetValue<bool>() == true) encoded = $"<strong>{encoded}</strong>";
        if (attributes?["italic"]?.GetValue<bool>() == true) encoded = $"<em>{encoded}</em>";
        if (attributes?["link"] is JsonValue linkValue)
        {
            var link = WebUtility.HtmlEncode(linkValue.GetValue<string>());
            encoded = $"<a href=\"{link}\">{encoded}</a>";
        }
        return encoded;
    }

    private static string FormatMarkdownInline(string value, JsonObject? attributes)
    {
        var result = value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);
        if (attributes?["bold"]?.GetValue<bool>() == true) result = $"**{result}**";
        if (attributes?["italic"]?.GetValue<bool>() == true) result = $"*{result}*";
        if (attributes?["link"] is JsonValue linkValue) result = $"[{result}]({linkValue.GetValue<string>()})";
        return result;
    }

    private static void EnsureValid(string value)
    {
        if (!TryValidate(value, out var error)) throw new ValidationException(error);
    }

    private static void CloseList(StringBuilder output, ref string? openList)
    {
        if (openList is null) return;
        output.Append("</").Append(openList).Append('>');
        openList = null;
    }

    private sealed record DeltaOperation(string Insert, JsonObject? Attributes);
}

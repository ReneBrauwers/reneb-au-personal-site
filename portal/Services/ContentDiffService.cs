using System.Text.Json;
using ReneB.Portal.Models;

namespace ReneB.Portal.Services;

public static class ContentDiffService
{
    public static IReadOnlyList<ContentDiffEntry> Compare(object published, object draft)
    {
        using var left = JsonDocument.Parse(JsonSerializer.Serialize(published, ContentTypeRegistry.JsonOptions));
        using var right = JsonDocument.Parse(JsonSerializer.Serialize(draft, ContentTypeRegistry.JsonOptions));
        var leftValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        var rightValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        Flatten(left.RootElement, string.Empty, leftValues);
        Flatten(right.RootElement, string.Empty, rightValues);
        return leftValues.Keys.Union(rightValues.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Where(path => !string.Equals(leftValues.GetValueOrDefault(path), rightValues.GetValueOrDefault(path), StringComparison.Ordinal))
            .Select(path => new ContentDiffEntry(path, leftValues.GetValueOrDefault(path), rightValues.GetValueOrDefault(path)))
            .ToArray();
    }

    private static void Flatten(JsonElement element, string path, Dictionary<string, string?> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) Flatten(property.Value, Join(path, property.Name), output);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray()) Flatten(item, $"{path}[{index++}]", output);
                break;
            default:
                output[path] = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();
                break;
        }
    }

    private static string Join(string path, string part) => string.IsNullOrEmpty(path) ? part : $"{path}.{part}";
}

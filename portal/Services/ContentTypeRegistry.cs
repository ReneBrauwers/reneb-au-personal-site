using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ReneB.Portal.Models;

namespace ReneB.Portal.Services;

public static class ContentTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
    {
        [ContentDocumentKeys.Home] = typeof(HomePageContent),
        [ContentDocumentKeys.SiteSettings] = typeof(SiteSettingsContent),
        [ContentDocumentKeys.RecruiterProfile] = typeof(PublicCandidateProfile),
        [ContentDocumentKeys.OpportunityProfile] = typeof(PrivateCandidateProfile),
        [ContentDocumentKeys.Privacy] = typeof(PrivacyNoticeContent),
        [ContentDocumentKeys.Discovery] = typeof(DiscoveryGuidanceContent)
    };

    public static string TypeName(string key) => GetType(key).Name;

    public static Type GetType(string key) => Types.TryGetValue(key, out var type)
        ? type
        : throw new ArgumentOutOfRangeException(nameof(key), "Unknown content document key.");

    public static object DeserializeAndValidate(string key, string json)
    {
        var value = JsonSerializer.Deserialize(json, GetType(key), JsonOptions)
            ?? throw new ValidationException("Content is empty.");
        ValidateGraph(value);
        return value;
    }

    public static void ValidateGraph(object value)
    {
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        ValidateNode(value, visited, value.GetType().Name);
        if (value is SiteSettingsContent settings)
        {
            if (!Uri.TryCreate(settings.UmamiScriptUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || !uri.AbsolutePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("The Umami script URL must be a plain HTTPS JavaScript URL without credentials, query or fragment values.");
            }
        }
    }

    private static void ValidateNode(object? value, HashSet<object> visited, string path)
    {
        if (value is null || value is string || value.GetType().IsValueType || !visited.Add(value)) return;
        Validator.ValidateObject(value, new ValidationContext(value), validateAllProperties: true);

        if (value is RichTextContent richText)
        {
            if (!RichTextDelta.TryValidate(richText.DeltaJson, out var error)) throw new ValidationException($"{path}: {error}");
            return;
        }
        if (value is System.Collections.IEnumerable values)
        {
            var index = 0;
            foreach (var item in values) ValidateNode(item, visited, $"{path}[{index++}]");
            return;
        }
        foreach (var property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(p => p.CanRead))
        {
            ValidateNode(property.GetValue(value), visited, $"{path}.{property.Name}");
        }
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };
}

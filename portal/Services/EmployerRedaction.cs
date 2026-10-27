using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ReneB.Portal.Services;

/// <summary>
/// The current employer is deliberately not published. New content never contains it, but content
/// documents that were stored before that decision may still do so. This removes it when public
/// content is read, so no redeploy-time data migration is needed and nothing re-publishes it.
/// </summary>
public static partial class EmployerRedaction
{
    public const string GenericPhrase = "regulated financial services";

    [GeneratedRegex(@"^\s*EMPLOYER(?:\s+(?:Corporate\s+Trust|Limited|Group))?\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex WholeValue();

    [GeneratedRegex(@"\bat\s+EMPLOYER(?:\s+(?:Corporate\s+Trust|Limited|Group))?\b", RegexOptions.CultureInvariant)]
    private static partial Regex AtEmployer();

    [GeneratedRegex(@"\bEMPLOYER(?:\s+(?:Corporate\s+Trust|Limited|Group))?\b", RegexOptions.CultureInvariant)]
    private static partial Regex AnyEmployer();

    public static bool Mentions(string? value) => !string.IsNullOrEmpty(value) && AnyEmployer().IsMatch(value);

    public static string Redact(string value)
    {
        if (!Mentions(value)) return value;
        if (WholeValue().IsMatch(value)) return "Enterprise architecture leadership";
        value = AtEmployer().Replace(value, "in " + GenericPhrase);
        return AnyEmployer().Replace(value, "a regulated financial-services organisation");
    }

    /// <summary>Redacts every string reachable from a public content document, in place.</summary>
    public static void Apply(object? content)
        => Walk(content, new HashSet<object>(ReferenceEqualityComparer.Instance));

    private static void Walk(object? node, HashSet<object> visited)
    {
        if (node is null || node is string || node.GetType().IsValueType || !visited.Add(node)) return;

        if (node is IList list && !list.IsReadOnly && !list.IsFixedSize)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] is string text) list[i] = Redact(text);
                else Walk(list[i], visited);
            }
            return;
        }

        foreach (var property in node.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0) continue;
            if (property.PropertyType == typeof(string))
            {
                if (property.CanWrite && property.GetValue(node) is string text) property.SetValue(node, Redact(text));
            }
            else if (!property.PropertyType.IsValueType)
            {
                Walk(property.GetValue(node), visited);
            }
        }
    }
}

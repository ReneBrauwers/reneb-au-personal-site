using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ReneB.Portal.Services;

/// <summary>
/// Removes host-configured terms (for example a current employer) from public content.
/// The terms come from the host-owned <c>PUBLIC_REDACT_TERMS</c> setting (comma or semicolon
/// separated) and are never stored in source. When the setting is empty this does nothing.
/// Content documents stored before a term was added are redacted when they are read, so no
/// re-publish or data migration is needed.
/// </summary>
public sealed class EmployerRedaction
{
    public const string SettingName = "PUBLIC_REDACT_TERMS";
    public const string GenericPhrase = "regulated financial services";

    private static volatile EmployerRedaction _current = new([]);

    private readonly Regex? _whole;
    private readonly Regex? _atTerm;
    private readonly Regex? _anyTerm;

    public EmployerRedaction(IEnumerable<string> terms)
    {
        var cleaned = terms
            .Select(term => term?.Trim() ?? string.Empty)
            .Where(term => term.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(term => term.Length)
            .ToArray();
        if (cleaned.Length == 0) return;

        var alternatives = string.Join("|", cleaned.Select(term =>
            string.Join(@"\s+", term.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape))));
        var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        _anyTerm = new Regex($@"(?<![\p{{L}}\p{{N}}])(?:{alternatives})(?![\p{{L}}\p{{N}}])", options, TimeSpan.FromSeconds(1));
        _atTerm = new Regex($@"\bat\s+(?:{alternatives})(?![\p{{L}}\p{{N}}])", options, TimeSpan.FromSeconds(1));
        _whole = new Regex($@"^\s*(?:{alternatives})\s*$", options, TimeSpan.FromSeconds(1));
    }

    /// <summary>The redactor configured for this process by <see cref="Configure"/>.</summary>
    public static EmployerRedaction Current => _current;

    /// <summary>Parses the raw setting value; null, empty or blank means no redaction.</summary>
    public static IReadOnlyList<string> ParseTerms(string? raw)
        => string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', ';', '\n', '\r'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static void Configure(string? raw) => _current = new EmployerRedaction(ParseTerms(raw));

    public bool IsEnabled => _anyTerm is not null;

    public bool Mentions(string? value) => _anyTerm is not null && !string.IsNullOrEmpty(value) && _anyTerm.IsMatch(value);

    public string Redact(string value)
    {
        if (_anyTerm is null || !_anyTerm.IsMatch(value)) return value;
        if (_whole!.IsMatch(value)) return "Enterprise architecture leadership";
        value = _atTerm!.Replace(value, "in " + GenericPhrase);
        return _anyTerm.Replace(value, "a regulated financial-services organisation");
    }

    /// <summary>Redacts every string reachable from a public content document, in place.</summary>
    public void Apply(object? content)
    {
        if (_anyTerm is null) return;
        Walk(content, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private void Walk(object? node, HashSet<object> visited)
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

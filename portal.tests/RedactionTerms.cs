using Xunit;

namespace ReneB.Portal.Tests;

/// <summary>
/// The terms that must stay out of public output are secret, so they are never in source.
/// Tests read them from the PUBLIC_REDACT_TERMS environment variable, or from the gitignored
/// <c>.redact-terms.local</c> file at the repository root. When neither exists, tests that need
/// the real terms are skipped.
/// </summary>
internal static class RedactionTerms
{
    public const string FileName = ".redact-terms.local";

    public static string? Raw { get; } = Load();

    public static IReadOnlyList<string> All { get; } = ReneB.Portal.Services.EmployerRedaction.ParseTerms(Raw);

    private static string? Load()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(ReneB.Portal.Services.EmployerRedaction.SettingName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate))
            {
                var text = File.ReadAllText(candidate);
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        return null;
    }
}

/// <summary>A fact that is skipped cleanly when no redaction terms are configured locally.</summary>
public sealed class RedactionFactAttribute : FactAttribute
{
    public RedactionFactAttribute()
    {
        if (RedactionTerms.All.Count == 0)
        {
            Skip = "PUBLIC_REDACT_TERMS is not set and .redact-terms.local is absent.";
        }
    }
}

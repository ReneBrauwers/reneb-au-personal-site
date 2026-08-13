using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using ReneB.Portal.Models;

namespace ReneB.Portal.Services;

public static class PublicProfileRenderer
{
    public static object ToCandidateDocument(PublicCandidateProfile profile, DiscoveryGuidanceContent? guidance = null) => new
    {
        schemaVersion = "1.0",
        candidateSupplied = true,
        lastReviewed = profile.LastReviewed,
        candidate = new
        {
            name = profile.CandidateName,
            location = "Australia",
            canonicalProfile = "https://reneb.au/recruiters",
            contact = "https://reneb.au/auth/register",
            headline = profile.Headline,
            currentRole = profile.CurrentRole,
            currentEmployer = profile.CurrentEmployer,
            professionalContext = profile.ProfessionalContext,
            summary = profile.Summary,
            demonstratedSignals = profile.DemonstratedSignals,
            rolesOfInterest = profile.RolesOfInterest,
            areasOfInterest = profile.AreasOfInterest,
            locationPreferences = profile.LocationPreferences,
            strongFitSignals = profile.StrongFitSignals,
            poorFitSignals = profile.PoorFitSignals
        },
        disclosure = new
        {
            compensation = (guidance ?? ContentDefaults.Discovery()).CompensationDisclosure,
            detailedAvailability = (guidance ?? ContentDefaults.Discovery()).AvailabilityDisclosure,
            resume = (guidance ?? ContentDefaults.Discovery()).ResumeDisclosure,
            agentAction = (guidance ?? ContentDefaults.Discovery()).MatchingGuidance
        }
    };

    public static string ToJson(PublicCandidateProfile profile, DiscoveryGuidanceContent? guidance = null) => JsonSerializer.Serialize(ToCandidateDocument(profile, guidance), JsonOptions);

    public static string ToMarkdown(PublicCandidateProfile profile, DiscoveryGuidanceContent? guidance = null)
    {
        guidance ??= ContentDefaults.Discovery();
        var output = new StringBuilder();
        output.AppendLine($"# {profile.CandidateName} — recruiter profile");
        output.AppendLine();
        output.AppendLine($"> Candidate-supplied. Last reviewed {profile.LastReviewed:dd MMMM yyyy}.");
        output.AppendLine();
        output.AppendLine(profile.Headline);
        output.AppendLine();
        output.AppendLine($"Current role: {profile.CurrentRole} at {profile.CurrentEmployer} ({profile.ProfessionalContext}).");
        output.AppendLine();
        output.AppendLine(profile.Summary);
        AppendList(output, "Demonstrated signals", profile.DemonstratedSignals);
        AppendList(output, "Roles of interest", profile.RolesOfInterest);
        AppendList(output, "Areas of interest", profile.AreasOfInterest);
        AppendList(output, "Location preferences", profile.LocationPreferences);
        AppendList(output, "Strong-fit mandates", profile.StrongFitSignals);
        AppendList(output, "Usually not suitable", profile.PoorFitSignals);
        output.AppendLine("## Verified access");
        output.AppendLine();
        output.AppendLine($"Compensation: {guidance.CompensationDisclosure}. Detailed availability: {guidance.AvailabilityDisclosure}. Résumé: {guidance.ResumeDisclosure}.");
        output.AppendLine();
        output.AppendLine("- Canonical profile: https://reneb.au/recruiters");
        output.AppendLine("- Request verified access: https://reneb.au/auth/register");
        return output.ToString();
    }

    public static string ToLlmsText(PublicCandidateProfile profile, DiscoveryGuidanceContent? guidance = null)
    {
        guidance ??= ContentDefaults.Discovery();
        var output = new StringBuilder();
        output.AppendLine($"# {profile.CandidateName}");
        output.AppendLine();
        output.AppendLine($"> {guidance.CandidateSuppliedNotice}");
        output.AppendLine();
        output.AppendLine($"{profile.Headline}. {profile.Summary}");
        output.AppendLine();
        output.AppendLine($"Current role: {profile.CurrentRole} at {profile.CurrentEmployer} ({profile.ProfessionalContext}).");
        AppendList(output, "Demonstrated signals", profile.DemonstratedSignals);
        AppendList(output, "Roles of interest", profile.RolesOfInterest);
        AppendList(output, "Areas of interest", profile.AreasOfInterest);
        AppendList(output, "Location preferences", profile.LocationPreferences);
        AppendList(output, "Strong-fit mandates", profile.StrongFitSignals);
        AppendList(output, "Usually not suitable", profile.PoorFitSignals);
        output.AppendLine("## Matching guidance");
        output.AppendLine();
        output.AppendLine(guidance.MatchingGuidance);
        output.AppendLine();
        output.AppendLine("- [Canonical recruiter profile](https://reneb.au/recruiters): Human-readable evidence and verified-access handoff");
        output.AppendLine("- [Structured candidate profile](https://reneb.au/candidate.json): Versioned JSON representation");
        output.AppendLine("- [Markdown candidate profile](https://reneb.au/recruiters/profile.md): Markdown representation");
        output.AppendLine("- [Request verified access](https://reneb.au/auth/register): Human mailbox verification for private criteria");
        output.AppendLine();
        output.AppendLine($"Last reviewed: {profile.LastReviewed:dd MMMM yyyy}");
        return output.ToString();
    }

    public static string ToJsonLd(PublicCandidateProfile profile)
    {
        var services = profile.RolesOfInterest
            .Select(role => (object)new Dictionary<string, object?>
            {
                ["@type"] = "Service",
                ["name"] = role
            })
            .ToArray();
        var demand = new Dictionary<string, object?>
        {
            ["@type"] = "Demand",
            ["name"] = profile.Headline,
            ["description"] = profile.Summary,
            ["itemOffered"] = services
        };
        var person = new Dictionary<string, object?>
        {
            ["@type"] = "Person",
            ["@id"] = "https://reneb.au/#rene-brauwers",
            ["name"] = profile.CandidateName,
            ["url"] = "https://reneb.au/",
            ["jobTitle"] = profile.CurrentRole,
            ["worksFor"] = new Dictionary<string, object?>
            {
                ["@type"] = "Organization",
                ["name"] = profile.CurrentEmployer
            },
            ["knowsAbout"] = profile.AreasOfInterest,
            ["seeks"] = demand
        };
        var document = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "ProfilePage",
            ["@id"] = "https://reneb.au/recruiters#profile",
            ["url"] = "https://reneb.au/recruiters",
            ["dateModified"] = profile.LastReviewed.ToString("yyyy-MM-dd"),
            ["mainEntity"] = person
        };
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    private static void AppendList(StringBuilder output, string heading, IEnumerable<string> values)
    {
        output.AppendLine();
        output.AppendLine($"## {heading}");
        output.AppendLine();
        foreach (var value in values)
        {
            output.AppendLine($"- {value}");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Default,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

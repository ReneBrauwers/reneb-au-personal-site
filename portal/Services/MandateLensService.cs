using System.Globalization;
using System.Text;
using ReneB.Portal.Models;

namespace ReneB.Portal.Services;

public sealed class MandateLensService
{
    public const int MaximumMandateLength = 2400;
    public const int MaximumNoteLength = 400;
    public const int MaximumRoleLength = 140;
    public const int MaximumPrivateMessageLength = 4000;

    private static readonly SignalDefinition[] SignalDefinitions =
    [
        new(
            "authority",
            "Enterprise authority",
            "The brief points to decision rights, target-state accountability or an enterprise-wide architecture mandate.",
            "What decisions does this role own, and where can it make a binding call?",
            ["enterprise architecture", "chief architect", "group architect", "design authority", "target state", "target-state", "technology strategy", "architecture function", "operating model", "decision rights", "enterprise-wide"],
            ["enterprise architecture", "enterprise", "target state", "authority", "strategy", "architecture function", "senior leaders"],
            ["enterprise architecture function", "matrix architecture leadership"]),
        new(
            "investment",
            "Investment and transformation",
            "The mandate includes portfolio choices, roadmaps, transformation sequencing or investment consequences.",
            "Which investment or sequencing decision must be materially different in the first 12 months?",
            ["investment", "portfolio", "roadmap", "business case", "transformation", "modernisation", "modernization", "sequencing", "cost optimisation", "cost optimization", "value case", "strategic planning"],
            ["investment", "portfolio", "roadmap", "business case", "transformation", "sequencing", "strategy"],
            ["investment planning", "business cases", "roadmaps and guardrails"]),
        new(
            "governance",
            "Governance, resilience and risk",
            "Risk, regulatory obligations or governance quality appear central to the outcome.",
            "Which risk, audit or resilience obligations are non-negotiable—and where is current governance slowing delivery?",
            ["governance", "risk", "regulatory", "regulated", "resilience", "privacy", "security", "audit", "control framework", "financial services", "banking", "insurance", "trust company"],
            ["governance", "risk", "regulatory", "regulated", "resilience", "privacy", "security", "audit"],
            ["technology governance", "risk and audit", "risk into roadmaps"]),
        new(
            "delivery",
            "Delivery and platform reality",
            "The role must connect strategy to Product, Engineering, platforms or modernisation delivery.",
            "How close is this mandate expected to stay to Product and Engineering after target-state decisions are made?",
            ["engineering", "product", "delivery", "platform", "cloud", "azure", "data platform", "integration", "api", "event-driven", "developer productivity", "engineering enablement", "legacy modernisation", "legacy modernization"],
            ["engineering", "product", "delivery", "platform", "cloud", "azure", "data", "integration", "api", "automation"],
            ["product and engineering", "cross business product engineering", "delivery squads"]),
        new(
            "ai",
            "Responsible AI adoption",
            "The brief needs accountable AI governance, enablement or platform decisions rather than a tooling-only answer.",
            "Is the AI mandate primarily governance, platform engineering, adoption, or accountable business change?",
            ["artificial intelligence", "ai", "agentic", "responsible ai", "ai governance", "machine learning", "model risk", "ai platform", "copilot", "intelligent automation"],
            ["ai", "agentic", "responsible ai", "automation", "governance", "platform"],
            ["responsible ai", "ai foundations", "agentic ai"]),
        new(
            "leadership",
            "Executive and practice leadership",
            "The mandate depends on executive influence, cross-functional leadership or building a stronger practice.",
            "Is success measured by a stronger function, better decisions across the matrix, or direct team delivery?",
            ["executive", "board", "c-suite", "leadership", "lead a team", "practice lead", "chapter lead", "stakeholder", "influence", "mentor", "build a team", "cross-functional"],
            ["executive", "senior leaders", "lead", "leadership", "function", "matrix", "cross-functional", "governance forums"],
            ["advises senior", "leads an enterprise", "matrix architecture leadership"])
    ];

    private static readonly GapDefinition[] GapDefinitions =
    [
        new("authority", "Decision rights are not explicit in the pasted text.", "Where does this role have authority, and which decisions remain advisory?"),
        new("investment", "The investment or business consequence is not explicit in the pasted text.", "What outcome, investment or constraint makes this mandate consequential now?"),
        new("delivery", "The interface with Product and Engineering is not described.", "How will strategy stay connected to delivery evidence and engineering feedback?")
    ];

    private static readonly FrictionDefinition[] FrictionDefinitions =
    [
        new("Pre-sales orientation", "The pasted text contains sales or quota-oriented language. Clarify whether trusted enterprise authority or commercial pre-sales is the real job.", ["pre-sales", "presales", "sales quota", "quota carrying", "account executive", "sales pipeline"]),
        new("Staff-augmentation model", "The pasted text suggests staff augmentation or utilisation-led consulting. Clarify whether the mandate carries durable authority and outcome ownership.", ["staff augmentation", "resource augmentation", "body shop", "bodyshop", "billable utilisation", "billable utilization"]),
        new("Delivery-only architecture", "The pasted text may describe an implementation-only architecture role. Clarify the enterprise scope, decision rights and target-state accountability.", ["solution architect", "project architect", "hands-on delivery only", "individual contributor only"])
    ];

    public MandateLensResult Analyse(string mandate, PublicCandidateProfile profile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mandate);
        ArgumentNullException.ThrowIfNull(profile);

        var normalizedMandate = Normalize(mandate);
        var matchedDefinitions = SignalDefinitions
            .Select(definition => new { Definition = definition, Hits = CountTerms(normalizedMandate, definition.MandateTerms) })
            .Where(item => item.Hits > 0)
            .OrderByDescending(item => item.Hits)
            .ThenBy(item => Array.IndexOf(SignalDefinitions, item.Definition))
            .Select(item => item.Definition)
            .ToArray();
        var matchedKeys = matchedDefinitions.Select(definition => definition.Key).ToHashSet(StringComparer.Ordinal);

        var usedEvidence = new HashSet<string>(StringComparer.Ordinal);
        var signals = matchedDefinitions
            .Select(definition => new { Definition = definition, Evidence = FindEvidence(profile, definition, usedEvidence) })
            .Where(item => item.Evidence is not null)
            .Take(5)
            .Select(item => new MandateLensSignal(
                item.Definition.Key,
                item.Definition.Label,
                item.Definition.Observation,
                item.Evidence!))
            .ToArray();

        var severeFriction = FrictionDefinitions
            .Where(definition => definition.Terms.Any(term => ContainsTerm(normalizedMandate, term)))
            .Where(definition => definition.Label != "Delivery-only architecture" || !matchedKeys.Contains("authority"))
            .Select(definition => definition.Detail)
            .ToList();

        foreach (var gap in GapDefinitions.Where(gap => !matchedKeys.Contains(gap.SignalKey)))
        {
            severeFriction.Add(gap.Observation);
        }

        var questions = matchedDefinitions.Select(definition => definition.Question)
            .Concat(GapDefinitions.Where(gap => !matchedKeys.Contains(gap.SignalKey)).Select(gap => gap.Question))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        var conclusion = severeFriction.Any(item => item.StartsWith("The pasted text contains", StringComparison.Ordinal)
            || item.StartsWith("The pasted text suggests", StringComparison.Ordinal)
            || item.StartsWith("The pasted text may describe", StringComparison.Ordinal))
                ? "Clarify the operating model before calling this a match."
                : signals.Length >= 4
                    ? "This mandate earns a focused first conversation."
                    : signals.Length >= 2
                        ? "There is credible overlap; test the decision rights early."
                        : "The brief needs sharper mandate detail before fit can be judged.";

        var summary = signals.Length == 0
            ? "No clear enterprise-mandate signal is explicit yet. The lens is withholding a fit claim until the brief names the authority, outcome or delivery consequence."
            : $"The strongest overlap sits across {JoinLabels(signals.Select(signal => signal.Label))}. The evidence below is candidate-supplied; it is a starting point for human review, not a ranking or fit score.";

        return new MandateLensResult(
            conclusion,
            summary,
            WorkingHypothesis(signals),
            signals,
            questions,
            severeFriction.Distinct(StringComparer.Ordinal).Take(3).ToArray());
    }

    public string ComposePrivateMessage(string? roleLabel, string mandate, string? note, MandateLensResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mandate);
        ArgumentNullException.ThrowIfNull(result);

        var trimmedRole = roleLabel?.Trim();
        var trimmedMandate = mandate.Trim();
        var trimmedNote = note?.Trim();
        if (trimmedRole?.Length > MaximumRoleLength)
        {
            throw new ArgumentOutOfRangeException(nameof(roleLabel), $"Role labels cannot exceed {MaximumRoleLength} characters.");
        }
        if (trimmedMandate.Length > MaximumMandateLength)
        {
            throw new ArgumentOutOfRangeException(nameof(mandate), $"Mandates cannot exceed {MaximumMandateLength} characters.");
        }
        if (trimmedNote?.Length > MaximumNoteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(note), $"Recruiter context cannot exceed {MaximumNoteLength} characters.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("Mandate Lens brief shared by a verified recruiter.");
        if (!string.IsNullOrWhiteSpace(trimmedRole))
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Role: {trimmedRole}");
        }
        builder.AppendLine(CultureInfo.InvariantCulture, $"Lens conclusion: {result.Conclusion}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Working hypothesis: {result.WorkingHypothesis}");
        if (result.Signals.Count > 0)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"Mandate signals: {string.Join(", ", result.Signals.Select(signal => signal.Label))}");
        }
        if (!string.IsNullOrWhiteSpace(trimmedNote))
        {
            builder.AppendLine();
            builder.AppendLine("Recruiter context:");
            builder.AppendLine(trimmedNote);
        }
        builder.AppendLine();
        builder.AppendLine("Pasted mandate:");
        builder.Append(trimmedMandate);

        if (builder.Length > MaximumPrivateMessageLength)
        {
            throw new InvalidOperationException("The complete Mandate Lens message exceeds the encrypted message limit.");
        }
        return builder.ToString();
    }

    private static string? FindEvidence(PublicCandidateProfile profile, SignalDefinition definition, HashSet<string> used)
    {
        var evidence = profile.DemonstratedSignals
            .Where(item => !used.Contains(item))
            .Select(item =>
            {
                var normalized = Normalize(item);
                var score = (10 * CountTerms(normalized, definition.PreferredEvidenceTerms))
                    + CountTerms(normalized, definition.EvidenceTerms);
                return new { Text = item, Score = score };
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Text, StringComparer.Ordinal)
            .FirstOrDefault(item => item.Score > 0)?.Text;

        if (evidence is not null)
        {
            used.Add(evidence);
        }
        return evidence;
    }

    private static string WorkingHypothesis(IReadOnlyCollection<MandateLensSignal> signals)
    {
        var keys = signals.Select(signal => signal.Key).ToHashSet(StringComparer.Ordinal);
        if (keys.Contains("ai") && keys.Contains("governance"))
        {
            return "Separate accountable AI decisions, platform enablement and adoption outcomes before selecting controls or tooling.";
        }
        if (keys.Contains("authority") && keys.Contains("investment"))
        {
            return "Map decision rights, funding gates and sequencing constraints that currently separate executive intent from delivery.";
        }
        if (keys.Contains("governance") && keys.Contains("delivery"))
        {
            return "Find where control design and delivery flow are fighting each other, then make the operating trade-off explicit.";
        }
        if (keys.Contains("delivery"))
        {
            return "Trace one consequential decision from business outcome to engineering evidence and expose where the feedback loop breaks.";
        }
        if (keys.Contains("authority"))
        {
            return "Start with the decisions this mandate must improve, who owns them and what evidence should change those decisions.";
        }
        return "Name the consequential decision, its business outcome and the authority required before choosing an architecture response.";
    }

    private static string JoinLabels(IEnumerable<string> labels)
    {
        var values = labels.Take(3).ToArray();
        return values.Length switch
        {
            0 => "no explicit themes",
            1 => values[0],
            2 => $"{values[0]} and {values[1]}",
            _ => $"{values[0]}, {values[1]} and {values[2]}"
        };
    }

    private static int CountTerms(string normalizedText, IEnumerable<string> terms)
        => terms.Count(term => ContainsTerm(normalizedText, term));

    private static bool ContainsTerm(string normalizedText, string term)
    {
        var needle = $" {Normalize(term).Trim()} ";
        var searchFrom = 0;
        while (searchFrom < normalizedText.Length)
        {
            var index = normalizedText.IndexOf(needle, searchFrom, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }
            if (!IsNegated(normalizedText.AsSpan(0, index)))
            {
                return true;
            }
            searchFrom = index + needle.Length - 1;
        }
        return false;
    }

    private static bool IsNegated(ReadOnlySpan<char> prefix)
    {
        var preceding = prefix.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(4).ToArray();
        for (var index = preceding.Length - 1; index >= 0; index--)
        {
            if (preceding[index] is "but" or "however" or "except")
            {
                return false;
            }
            if (preceding[index] is not ("no" or "not" or "without" or "neither" or "nor" or "exclude" or "excluding" or "lacks" or "lacking"))
            {
                continue;
            }
            return preceding[index] != "not" || index + 1 >= preceding.Length || preceding[index + 1] != "only";
        }
        return false;
    }

    private static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length + 2).Append(' ');
        var previousWasSpace = true;
        foreach (var character in normalized)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
            }
            else if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }
        if (!previousWasSpace)
        {
            builder.Append(' ');
        }
        return builder.ToString();
    }

    private sealed record SignalDefinition(
        string Key,
        string Label,
        string Observation,
        string Question,
        string[] MandateTerms,
        string[] EvidenceTerms,
        string[] PreferredEvidenceTerms);

    private sealed record GapDefinition(string SignalKey, string Observation, string Question);
    private sealed record FrictionDefinition(string Label, string Detail, string[] Terms);
}

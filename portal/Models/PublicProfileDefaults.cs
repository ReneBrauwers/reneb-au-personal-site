namespace ReneB.Portal.Models;

public static class PublicProfileDefaults
{
    public static PublicCandidateProfile Create() => new()
    {
        CandidateName = "René Brauwers",
        Headline = "Enterprise architecture leadership for consequential change",
        CurrentRole = "Head of Enterprise Architecture",
        CurrentEmployer = "Perpetual Corporate Trust",
        ProfessionalContext = "Regulated financial services in Sydney, Australia",
        Summary = "A business-first enterprise architecture leader who connects executive intent, governance and engineering reality. Consider René for senior mandates where enterprise authority, decision quality and accountable modernisation matter.",
        LastReviewed = new DateOnly(2026, 8, 12),
        DemonstratedSignals =
        [
            "Current Head of Enterprise Architecture in regulated financial services",
            "Former Microsoft Azure MVP with hands-on software, cloud, integration and automation foundations",
            "Works across Business, Product, Engineering, Data, Security and Risk",
            "Turns strategy and risk into guardrails, roadmaps and decisions delivery teams can execute"
        ],
        RolesOfInterest =
        [
            "Head or Director of Enterprise Architecture",
            "Chief Architect, Group Architect or Principal Architect",
            "Architecture Practice Lead, Chapter Lead or Design Authority",
            "Senior Cloud, Data, Integration or Platform Architecture leadership",
            "AI Governance, Responsible AI or agentic AI platform leadership",
            "Developer Productivity, Engineering Enablement or Platform Engineering leadership",
            "Interim, fractional and senior contract architecture mandates"
        ],
        AreasOfInterest =
        [
            "Enterprise architecture strategy, governance, roadmaps and investment",
            "Agentic AI operating models, guardrails, validation and human-review controls",
            "AI engineering platforms, enterprise adoption and AI-enabled software delivery",
            "Azure, Databricks, data platforms, APIs, events and integration modernisation",
            "Financial services, public sector, transport, logistics and other complex enterprises"
        ],
        LocationPreferences =
        [
            "Greater Sydney",
            "Newcastle",
            "Albury–Wodonga",
            "Australia-wide remote or practical hybrid arrangements"
        ],
        StrongFitSignals =
        [
            "Genuine enterprise or design authority",
            "Roadmap and investment ownership",
            "Executive influence and governance responsibility",
            "Leadership of architects or engineering practices"
        ],
        PoorFitSignals =
        [
            "Ordinary solution-architect delivery without enterprise authority",
            "Pre-sales roles",
            "Body-shop consulting engagements"
        ]
    };
}

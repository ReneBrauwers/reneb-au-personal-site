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
        Summary = "A business-first enterprise architecture leader who connects executive intent, investment, governance and engineering reality. René leads an enterprise architecture function, advises senior leaders and stays close enough to delivery to test strategy in practice. Consider him for senior mandates where enterprise authority, decision quality and accountable modernisation matter.",
        LastReviewed = new DateOnly(2026, 8, 13),
        DemonstratedSignals =
        [
            "Current Head of Enterprise Architecture in regulated financial services",
            "Leads an enterprise architecture function and provides matrix architecture leadership across multiple delivery squads",
            "Advises senior business and technology leaders, engages executive and governance forums, and contributes to annual investment planning and business cases",
            "Established technology governance spanning architecture review, standards, decisions, exceptions, risk and audit traceability",
            "Works across Business, Product, Engineering, Data, Security and Risk, turning strategy and risk into roadmaps and guardrails teams can execute",
            "Former Microsoft Azure MVP with hands-on software, cloud, integration, data, automation and responsible-AI foundations"
        ],
        RolesOfInterest =
        [
            "Head or Director of Enterprise Architecture",
            "Chief Architect, Group Architect or Principal Architect",
            "Architecture Practice Lead, Chapter Lead or Design Authority",
            "Senior Cloud, Data, Integration or Platform Architecture leadership",
            "Technology strategy, transformation or governance leadership",
            "AI Governance, Responsible AI or agentic AI platform leadership",
            "Developer Productivity, Engineering Enablement or Platform Engineering leadership",
            "Interim, fractional and senior contract architecture mandates"
        ],
        AreasOfInterest =
        [
            "Enterprise architecture strategy, governance, roadmaps and investment",
            "Technology investment planning, business cases and target-state sequencing",
            "Operational resilience, privacy, security and design risk in regulated enterprises",
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
            "Genuine enterprise or design authority with target-state accountability",
            "Roadmap ownership, investment sequencing and business-case influence",
            "Executive and governance forum engagement with cross-functional leadership",
            "Leadership of architecture or engineering practices with a short feedback loop to delivery",
            "Regulated environments where resilience, privacy, security and risk must be designed in"
        ],
        PoorFitSignals =
        [
            "Ordinary solution-architect delivery without enterprise authority",
            "Pre-sales roles",
            "Body-shop consulting engagements"
        ]
    };
}

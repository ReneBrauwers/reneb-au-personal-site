namespace ReneB.Portal.Models;

/// <summary>
/// Defaults for homepage sections added after the content studio was introduced. They are used as
/// property initializers so previously published home documents gain these sections on upgrade,
/// and they remain editable (and overridable) through the content studio.
/// </summary>
public static class HomeSectionDefaults
{
    public const string SystemsEyebrow = "Systems thinking";
    public const string SystemsHeading = "Fix the system, not the symptom.";

    public static RichTextContent SystemsIntroduction() => RichTextContent.FromParagraphs(
        "Most problems that reach a decision forum are symptoms. I look for the loop that produces them, so the fix holds and the next decision gets easier rather than harder.");

    public static List<ContentCard> SystemsPractices() =>
    [
        Card("Root cause before the fix", "Trace a problem back to what keeps producing it before choosing the remedy. A fix that skips the cause tends to come back with interest."),
        Card("See how the pieces connect", "People, process, platforms and data behave as one system. Mapping the dependencies stops a local change from creating a distant failure."),
        Card("Trade-offs in the open", "Cost, risk, reversibility and operating impact sit side by side, so the people who are accountable can choose with their eyes open."),
        Card("Evidence over gut", "Tests, telemetry and real usage settle the argument. Opinion can start the conversation; evidence should finish it."),
        Card("Prevention over recovery", "Guardrails, automation and short feedback loops stop the same incident from happening twice. Recovery still matters; it just shouldn’t be the plan.")
    ];

    public const string ProjectsNavigationLabel = "Projects";
    public const string ProjectsEyebrow = "Things I’ve built";
    public const string ProjectsHeading = "The builder never left.";

    public static RichTextContent ProjectsIntroduction() => RichTextContent.FromParagraphs(
        "Personal projects built in my own time, described here rather than linked. They keep my advice honest about what it takes to design, test, ship and run a working system.");

    public const string RecruiterContactNote = "Recruiters and talent partners: I don't publish an email address. Sign in to the recruiter portal (or request verified access) to get in touch and view my full profile and resume.";
    public const string RecruiterSignInAction = "Recruiter sign in";
    public const string RecruiterRequestAccessAction = "Request recruiter access";

    public static List<ProjectCard> Projects() =>
    [
        new()
        {
            Heading = "A browser maze game",
            Status = "Live",
            Detail = "An immersive maze game for phone and desktop, built on a .NET engine that procedurally generates mazes as JSON blueprints. Free play needs no account, download or payment; optional blockchain features sit behind independent release flags."
        },
        new()
        {
            Heading = "A practice-management prototype for a veterinary clinic",
            Status = "Unreleased prototype",
            Detail = "Born from a conversation with a mate who is a vet: a practice operating system for a single clinic, with the animal as the clinical spine. An offline-capable field app for staff, voice-drafted notes that a person reviews, and a separate portal for owners. Staff make and sign every clinical decision."
        },
        new()
        {
            Heading = "An automated asset-accumulation system",
            Status = "Personal use",
            Detail = "A deterministic, auditable, fail-closed system that accumulates a digital asset on a public ledger. Strategy, risk, signing, submission and reconciliation never depend on an LLM, and it runs only from accepted, immutable container images."
        },
        new()
        {
            Heading = "A character-creation and catalogue platform",
            Status = "Work in progress",
            Detail = "A container-first .NET and React platform for generating and curating characters with AI, with a prompt studio that audits every rendered prompt, encrypted provider secrets, multi-factor sign-in and privacy controls such as customer export and erasure."
        },
        new()
        {
            Heading = "A sensor-driven room pet",
            Status = "Personal use",
            Detail = "A small ESP32 device with air-quality, CO2 and climate sensors that gives a room a personality and nudges you to ventilate, heat or cool. A self-hosted .NET control centre manages telemetry, character profiles and signed over-the-air updates."
        },
        new()
        {
            Heading = "Crypto tax and portfolio tooling",
            Status = "Work in progress",
            Detail = "Tools that reconcile crypto transactions and estimate Australian capital gains: FIFO lot accounting across the July to June financial year, the 12-month discount, strict validation with per-transaction traces, and historical AUD price downloads."
        },
        new()
        {
            Heading = "This website",
            Status = "Live",
            Detail = "ASP.NET Core Razor Pages behind an nginx gateway, a governed content studio, a privacy-first recruiter portal, and CI that tests and scans every change before publishing container images."
        },
        new()
        {
            Heading = "Enterprise integration tooling",
            Status = "2010 to 2017",
            Detail = "Years of .NET integration side projects: custom mapping functions and REST message inspectors for an enterprise integration platform, a dynamic WCF service host and invoker, and a cloud orders API demo built for a community integration event."
        }
    ];

    private static ContentCard Card(string heading, string body) => new()
    {
        Heading = heading,
        Body = RichTextContent.FromParagraphs(body)
    };
}

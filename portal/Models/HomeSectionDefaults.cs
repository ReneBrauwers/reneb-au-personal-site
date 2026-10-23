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
            Heading = "Maze Realm",
            Status = "Live",
            Detail = "An immersive browser maze game for phone and desktop, built on a .NET maze generator that produces JSON blueprints. Free play needs no account, wallet, download or payment; optional XRPL Testnet features sit behind independent release flags."
        },
        new()
        {
            Heading = "Equine Vet OS",
            Status = "Unreleased prototype",
            Detail = "Born from a conversation with a mate who is a horse vet: a practice operating system for a single equine clinic, with the horse as the clinical spine. An offline-capable staff field app, voice-drafted notes that a person reviews, and a separate owner and trainer portal. Staff make and sign every clinical decision."
        },
        new()
        {
            Heading = "XRPL Accumulator",
            Status = "Personal use",
            Detail = "A deterministic, auditable, fail-closed system for accumulating an asset on the XRP Ledger. Strategy, risk, signing, submission and reconciliation never depend on an LLM, and it runs from accepted, immutable container images."
        },
        new()
        {
            Heading = "reneb.au",
            Status = "Live",
            Detail = "This site: ASP.NET Core Razor Pages behind an nginx gateway, a governed content studio, a privacy-first recruiter portal, and CI that validates every change before publishing container images."
        },
        new()
        {
            Heading = "CryptoTaxMan",
            Status = "Work in progress",
            Detail = "A Blazor WebAssembly app to manage and enrich crypto transactions and estimate capital gains with basic rules, plus a console tool that downloads AUD exchange rates from CoinGecko, Yahoo Finance and Sologenic."
        },
        new()
        {
            Heading = "Integration toolkit",
            Status = "2010 to 2017",
            Detail = "Years of .NET integration side projects: custom BizTalk functoids, a REST behaviour and message inspectors for BizTalk WCF endpoints, a dynamic WCF service host and invoker, and an Azure orders API demo built for Global Integration Bootcamp 2017 in Sydney."
        }
    ];

    private static ContentCard Card(string heading, string body) => new()
    {
        Heading = heading,
        Body = RichTextContent.FromParagraphs(body)
    };
}

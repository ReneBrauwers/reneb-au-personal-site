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
        "Personal projects built in my own time. They keep my advice honest about what it takes to design, test and ship a working system.");

    public const string ProjectsGitHubAction = "More on GitHub";

    public static List<ProjectCard> Projects() =>
    [
        new()
        {
            Heading = "Maze Realm",
            Status = "Live",
            Detail = "An immersive browser maze game for phone and desktop. Free play needs no account, wallet, download or payment; optional XRPL Testnet features sit behind independent release flags.",
            LinkUrl = "https://mazerealm.com/",
            LinkLabel = "Play at mazerealm.com"
        },
        new()
        {
            Heading = "Equine Vet OS",
            Status = "Unreleased prototype",
            Detail = "Born from a conversation with a mate who is a horse vet: a practice operating system for a single equine clinic, with the horse as the clinical spine. An offline-capable staff field app, voice-drafted notes that a person reviews, and a separate owner and trainer portal. Staff make and sign every clinical decision."
        },
        new()
        {
            Heading = "reneb.au",
            Status = "Live",
            Detail = "This site: ASP.NET Core Razor Pages behind an nginx gateway, a governed content studio, a privacy-first recruiter portal, and CI that validates every change before publishing container images.",
            LinkUrl = "https://github.com/ReneBrauwers/reneb-au-personal-site",
            LinkLabel = "Source on GitHub"
        },
        new()
        {
            Heading = "CryptoTaxMan",
            Status = "Work in progress",
            Detail = "A Blazor WebAssembly app to manage and enrich crypto transactions and estimate capital gains with basic rules, plus a console tool that downloads AUD exchange rates from CoinGecko, Yahoo Finance and Sologenic.",
            LinkUrl = "https://github.com/ReneBrauwers/CryptoTaxMan",
            LinkLabel = "Source on GitHub"
        }
    ];

    private static ContentCard Card(string heading, string body) => new()
    {
        Heading = heading,
        Body = RichTextContent.FromParagraphs(body)
    };
}

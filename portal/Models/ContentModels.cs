using System.ComponentModel.DataAnnotations;

namespace ReneB.Portal.Models;

public static class ContentDocumentKeys
{
    public const string Home = "home";
    public const string SiteSettings = "site-settings";
    public const string RecruiterProfile = "recruiter-profile";
    public const string OpportunityProfile = "opportunity-profile";
    public const string Privacy = "privacy";
    public const string Discovery = "machine-discovery";

    public static readonly IReadOnlyList<string> All =
    [
        Home, SiteSettings, RecruiterProfile, OpportunityProfile, Privacy, Discovery
    ];
}

public sealed class RichTextContent
{
    [Required, StringLength(100_000)]
    public string DeltaJson { get; set; } = "{\"ops\":[{\"insert\":\"\\n\"}]}";

    public static RichTextContent FromParagraphs(params string[] paragraphs)
        => new() { DeltaJson = RichTextDelta.CreateParagraphs(paragraphs) };
}

public sealed class ContentCard
{
    [Required, StringLength(120)] public string Heading { get; set; } = string.Empty;
    [Required] public RichTextContent Body { get; set; } = new();
}

public sealed class ProofItem
{
    [Required, StringLength(120)] public string Heading { get; set; } = string.Empty;
    [Required, StringLength(160)] public string Detail { get; set; } = string.Empty;
}

public sealed class HomePageContent
{
    [Required, StringLength(120)] public string HeaderDescriptor { get; set; } = string.Empty;
    [Required, StringLength(120)] public string PerspectiveNavigationLabel { get; set; } = string.Empty;
    [Required, StringLength(120)] public string RecruiterNavigationLabel { get; set; } = string.Empty;
    [Required, StringLength(120)] public string HeroEyebrow { get; set; } = string.Empty;
    [Required, StringLength(180)] public string HeroHeadingLineOne { get; set; } = string.Empty;
    [Required, StringLength(180)] public string HeroHeadingLineTwo { get; set; } = string.Empty;
    [Required] public RichTextContent HeroIntroduction { get; set; } = new();
    [Required, StringLength(120)] public string HeroPrimaryAction { get; set; } = string.Empty;
    [Required, StringLength(120)] public string HeroSecondaryAction { get; set; } = string.Empty;
    [Required, MinLength(1), MaxLength(6)] public List<ProofItem> ProofItems { get; set; } = [];
    [Required, StringLength(300)] public string HeroImageAlt { get; set; } = string.Empty;
    [Required, StringLength(200)] public string HeroImageCaption { get; set; } = string.Empty;

    [Required, StringLength(120)] public string ContributionEyebrow { get; set; } = string.Empty;
    [Required, StringLength(180)] public string ContributionHeading { get; set; } = string.Empty;
    [Required, MinLength(1), MaxLength(6)] public List<ContentCard> Contributions { get; set; } = [];

    [Required, StringLength(120)] public string FoundationEyebrow { get; set; } = string.Empty;
    [Required, StringLength(180)] public string FoundationHeading { get; set; } = string.Empty;
    [Required] public RichTextContent FoundationBody { get; set; } = new();

    [Required, StringLength(120)] public string PerspectiveEyebrow { get; set; } = string.Empty;
    [Required, StringLength(180)] public string PerspectiveHeading { get; set; } = string.Empty;
    [Required] public RichTextContent PerspectiveIntroduction { get; set; } = new();
    [Required, MinLength(1), MaxLength(6)] public List<ContentCard> Principles { get; set; } = [];
    [Required, StringLength(240)] public string PullQuote { get; set; } = string.Empty;
    [Required, StringLength(120)] public string PullQuoteAttribution { get; set; } = string.Empty;

    [Required, StringLength(120)] public string AiEyebrow { get; set; } = string.Empty;
    [Required, StringLength(180)] public string AiHeading { get; set; } = string.Empty;
    [Required] public RichTextContent AiBody { get; set; } = new();

    [Required, StringLength(120)] public string ClosingEyebrow { get; set; } = string.Empty;
    [Required, StringLength(180)] public string ClosingHeading { get; set; } = string.Empty;
    [Required] public RichTextContent ClosingBody { get; set; } = new();
    [Required, StringLength(120)] public string ClosingPrimaryAction { get; set; } = string.Empty;
    [Required, StringLength(120)] public string ClosingSecondaryAction { get; set; } = string.Empty;
}

public sealed class SiteSettingsContent
{
    [Required, StringLength(100)] public string DisplayName { get; set; } = "René Brauwers";
    [Required, StringLength(120)] public string Location { get; set; } = "Sydney, Australia";
    [Required, StringLength(180)] public string SeoTitle { get; set; } = string.Empty;
    [Required, StringLength(320)] public string MetaDescription { get; set; } = string.Empty;
    [Required, StringLength(180)] public string SocialTitle { get; set; } = string.Empty;
    [Required, StringLength(320)] public string SocialDescription { get; set; } = string.Empty;
    [Required, Url, StringLength(300)] public string SocialImageUrl { get; set; } = string.Empty;
    [Required, Url, StringLength(300)] public string LinkedInUrl { get; set; } = string.Empty;
    [Required, Url, StringLength(300)] public string XUrl { get; set; } = string.Empty;
    [Required, StringLength(80)] public string XHandle { get; set; } = string.Empty;
    [Required, StringLength(160)] public string FooterNotice { get; set; } = string.Empty;
    [StringLength(300)] public string AnalyticsNotice { get; set; } = string.Empty;
    public bool AnalyticsEnabled { get; set; } = true;
    [Required, Url, StringLength(300)] public string UmamiScriptUrl { get; set; } = "https://stats.reneb.au/script.js";
    [Required, RegularExpression("^[0-9a-fA-F-]{36}$")] public string UmamiWebsiteId { get; set; } = "55c627ba-826f-4472-9479-f1279071488c";
    [Required, RegularExpression("^[A-Za-z0-9.-]+(?:,[A-Za-z0-9.-]+)*$")] public string UmamiDomains { get; set; } = "reneb.au";
    public bool UmamiExcludeSearch { get; set; } = true;
    public bool UmamiDoNotTrack { get; set; } = true;
}

public sealed class PrivacyNoticeContent
{
    [Required, StringLength(120)] public string Eyebrow { get; set; } = "Privacy notice";
    [Required, StringLength(220)] public string Heading { get; set; } = string.Empty;
    [Required] public RichTextContent WhatIsCollected { get; set; } = new();
    [Required] public RichTextContent WhyItIsCollected { get; set; } = new();
    [Required] public RichTextContent HowItIsHandled { get; set; } = new();
    [Required] public RichTextContent AccessRetentionDeletion { get; set; } = new();
    [Required] public RichTextContent YourChoice { get; set; } = new();
    [Required, DataType(DataType.Date)] public DateOnly LastReviewed { get; set; }
}

public sealed class DiscoveryGuidanceContent
{
    [Required, StringLength(300)] public string CandidateSuppliedNotice { get; set; } = string.Empty;
    [Required, StringLength(1800)] public string MatchingGuidance { get; set; } = string.Empty;
    [Required, StringLength(300)] public string CompensationDisclosure { get; set; } = string.Empty;
    [Required, StringLength(300)] public string AvailabilityDisclosure { get; set; } = string.Empty;
    [Required, StringLength(300)] public string ResumeDisclosure { get; set; } = string.Empty;
}

public sealed record ContentDocumentRecord(
    string Key,
    string ContentType,
    long DraftRevision,
    long PublishedRevision,
    DateTimeOffset UpdatedAt,
    DateTimeOffset PublishedAt);

public sealed record ContentRevisionRecord(
    Guid Id,
    string DocumentKey,
    long Revision,
    int SchemaVersion,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    bool IsDraft,
    bool IsPublished);

public sealed record ContentSnapshot<T>(T Content, long Revision, DateTimeOffset UpdatedAt);

public sealed record ContentDiffEntry(string Path, string? PublishedValue, string? DraftValue);

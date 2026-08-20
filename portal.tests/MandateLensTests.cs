using ReneB.Portal.Models;
using ReneB.Portal.Services;

namespace ReneB.Portal.Tests;

public sealed class MandateLensTests
{
    private readonly MandateLensService _service = new();

    [Fact]
    public void ConsequentialMandateProducesEvidenceLedConversationBriefWithoutScore()
    {
        var profile = PublicProfileDefaults.Create();

        var result = _service.Analyse(
            "Director-level enterprise architecture and design authority for a regulated financial-services transformation. " +
            "The mandate owns target-state roadmaps, investment sequencing, responsible AI governance and the feedback loop with Product and Engineering.",
            profile);

        Assert.Equal("This mandate earns a focused first conversation.", result.Conclusion);
        Assert.Contains(result.Signals, signal => signal.Key == "authority");
        Assert.Contains(result.Signals, signal => signal.Key == "investment");
        Assert.Contains(result.Signals, signal => signal.Key == "governance");
        Assert.Contains(result.Signals, signal => signal.Key == "delivery");
        Assert.Contains(result.Signals, signal => signal.Key == "ai");
        Assert.All(result.Signals, signal => Assert.Contains(signal.Evidence, profile.DemonstratedSignals));
        Assert.Contains("responsible-AI", result.Signals.Single(signal => signal.Key == "ai").Evidence, StringComparison.Ordinal);
        Assert.Contains("investment planning", result.Signals.Single(signal => signal.Key == "investment").Evidence, StringComparison.Ordinal);
        Assert.Equal(3, result.Questions.Count);
        Assert.DoesNotContain('%', result.Summary);
    }

    [Fact]
    public void CommercialOrDeliveryOnlyLanguageIsSurfacedAsFrictionNotHiddenByKeywordOverlap()
    {
        var result = _service.Analyse(
            "A quota-carrying pre-sales solution architect supporting the sales pipeline, customer demonstrations and cloud delivery as an individual contributor only.",
            PublicProfileDefaults.Create());

        Assert.Equal("Clarify the operating model before calling this a match.", result.Conclusion);
        Assert.Contains(result.Friction, item => item.Contains("sales or quota", StringComparison.Ordinal));
        Assert.Contains(result.Friction, item => item.Contains("implementation-only", StringComparison.Ordinal));
    }

    [Fact]
    public void GenericBriefWithholdsFitClaimAndNamesMissingDecisionContext()
    {
        var result = _service.Analyse(
            "We are looking for an experienced technology professional to join a growing organisation and work with stakeholders on several important initiatives.",
            PublicProfileDefaults.Create());

        Assert.Equal("The brief needs sharper mandate detail before fit can be judged.", result.Conclusion);
        Assert.Empty(result.Signals);
        Assert.Contains(result.Friction, item => item.Contains("Decision rights", StringComparison.Ordinal));
        Assert.Contains(result.Friction, item => item.Contains("business consequence", StringComparison.Ordinal));
        Assert.Contains(result.Friction, item => item.Contains("Product and Engineering", StringComparison.Ordinal));
    }

    [Fact]
    public void NegatedLanguageDoesNotBecomePositiveOverlapOrCommercialFriction()
    {
        var result = _service.Analyse(
            "This mandate has no decision rights, no investment ownership, no governance accountability, " +
            "no Product or Engineering responsibility, and is explicitly not pre-sales or quota carrying.",
            PublicProfileDefaults.Create());

        Assert.Empty(result.Signals);
        Assert.Equal("The brief needs sharper mandate detail before fit can be judged.", result.Conclusion);
        Assert.DoesNotContain(result.Friction, item => item.Contains("sales or quota", StringComparison.Ordinal));
    }

    [Fact]
    public void NegationFollowsClauseScopeWithoutSuppressingPositiveFraming()
    {
        var negative = _service.Analyse(
            "This role does not have any responsibility for Product delivery.",
            PublicProfileDefaults.Create());
        var positive = _service.Analyse(
            "No ambiguity surrounds explicit decision rights and target-state accountability.",
            PublicProfileDefaults.Create());

        Assert.DoesNotContain(negative.Signals, signal => signal.Key == "delivery");
        Assert.Contains(positive.Signals, signal => signal.Key == "authority");
    }

    [Fact]
    public void UnrelatedPublishedClaimsAreNeverUsedAsCandidateEvidence()
    {
        var profile = new PublicCandidateProfile
        {
            Summary = "A published profile with no evidence relevant to the mandate under test.",
            DemonstratedSignals = ["Regularly speaks at professional community events and mentors early-career practitioners."]
        };

        var result = _service.Analyse(
            "Enterprise design authority with target-state accountability and investment roadmap ownership for a consequential transformation.",
            profile);

        Assert.Empty(result.Signals);
        Assert.DoesNotContain("community events", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void RelevantProfileSummaryRemainsEligibleEvidence()
    {
        var profile = new PublicCandidateProfile
        {
            Summary = "Owns enterprise design authority, target-state accountability and technology strategy.",
            DemonstratedSignals = []
        };

        var result = _service.Analyse(
            "Enterprise design authority with target-state accountability for technology strategy.",
            profile);

        var signal = Assert.Single(result.Signals);
        Assert.Equal("authority", signal.Key);
        Assert.Equal(profile.Summary, signal.Evidence);
    }

    [Fact]
    public void DisplayCapDoesNotCreateFalseGapsForMatchedCategories()
    {
        var result = _service.Analyse(
            "Enterprise-wide chief architect and design authority owning target-state strategy, investment portfolios, business cases and transformation roadmaps; " +
            "regulated governance, risk, audit and resilience; responsible AI, agentic AI and AI platform accountability; executive board leadership, cross-functional influence and practice leadership; plus Product delivery.",
            PublicProfileDefaults.Create());

        Assert.Equal(5, result.Signals.Count);
        Assert.DoesNotContain(result.Friction, item => item.Contains("Product and Engineering is not described", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Friction, item => item.Contains("Decision rights are not explicit", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Friction, item => item.Contains("investment or business consequence is not explicit", StringComparison.Ordinal));
    }

    [Fact]
    public void SharedBriefIsCompleteBoundedAndCarriesTheHumanContext()
    {
        var mandate = new string('a', MandateLensService.MaximumMandateLength);
        var note = new string('b', MandateLensService.MaximumNoteLength);
        var result = _service.Analyse("Enterprise architecture design authority and investment roadmap ownership.", PublicProfileDefaults.Create());

        var message = _service.ComposePrivateMessage("Chief Architect", mandate, note, result);

        Assert.True(message.Length <= MandateLensService.MaximumPrivateMessageLength);
        Assert.Contains("Role: Chief Architect", message, StringComparison.Ordinal);
        Assert.Contains("Working hypothesis:", message, StringComparison.Ordinal);
        Assert.Contains("Recruiter context:", message, StringComparison.Ordinal);
        Assert.Contains("Pasted mandate:", message, StringComparison.Ordinal);
        Assert.EndsWith(mandate, message, StringComparison.Ordinal);
        Assert.DoesNotContain('…', message);
    }

    [Fact]
    public void SharedBriefRejectsInputThatCannotFitTheCompleteMessageContract()
    {
        var result = _service.Analyse("Enterprise architecture design authority and investment roadmap ownership.", PublicProfileDefaults.Create());

        Assert.Throws<ArgumentOutOfRangeException>(() => _service.ComposePrivateMessage(
            "Chief Architect",
            new string('a', MandateLensService.MaximumMandateLength + 1),
            null,
            result));
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.ComposePrivateMessage(
            "Chief Architect",
            "Enterprise architecture design authority and investment roadmap ownership.",
            new string('b', MandateLensService.MaximumNoteLength + 1),
            result));
    }
}

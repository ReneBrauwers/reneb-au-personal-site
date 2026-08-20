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

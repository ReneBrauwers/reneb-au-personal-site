namespace ReneB.Portal.Models;

public sealed record MandateLensSignal(
    string Key,
    string Label,
    string Observation,
    string Evidence);

public sealed record MandateLensResult(
    string Conclusion,
    string Summary,
    string WorkingHypothesis,
    IReadOnlyList<MandateLensSignal> Signals,
    IReadOnlyList<string> Questions,
    IReadOnlyList<string> Friction);

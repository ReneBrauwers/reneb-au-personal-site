namespace ReneB.Portal.Security;

using ReneB.Portal.Models;

public static class DomainRiskClassifier
{
    public static DomainRisk Classify(
        string email,
        IEnumerable<string>? untrustedEmailDomains = null,
        IEnumerable<string>? disposableEmailDomains = null)
    {
        var separator = email.LastIndexOf('@');
        if (separator < 1 || separator == email.Length - 1)
        {
            return DomainRisk.Disposable;
        }

        var domain = email[(separator + 1)..].Trim().TrimEnd('.').ToLowerInvariant();
        if (domain.Length == 0)
        {
            return DomainRisk.Disposable;
        }
        if (MatchesConfiguredDomain(domain, disposableEmailDomains))
        {
            return DomainRisk.Disposable;
        }

        if (MatchesConfiguredDomain(domain, untrustedEmailDomains))
        {
            return DomainRisk.Free;
        }

        return DomainRisk.Business;
    }

    private static bool MatchesConfiguredDomain(string domain, IEnumerable<string>? configuredDomains)
        => configuredDomains?.Any(candidate =>
        {
            var configuredDomain = candidate.Trim().TrimStart('@').TrimEnd('.');
            return configuredDomain.Length > 0
                && (string.Equals(domain, configuredDomain, StringComparison.OrdinalIgnoreCase)
                    || domain.EndsWith($".{configuredDomain}", StringComparison.OrdinalIgnoreCase));
        }) == true;
}

namespace ReneB.Portal.Security;

using ReneB.Portal.Models;

public static class DomainRiskClassifier
{
    private static readonly HashSet<string> FreeDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "gmail.com", "googlemail.com", "outlook.com", "hotmail.com", "live.com", "icloud.com", "me.com",
        "yahoo.com", "yahoo.com.au", "proton.me", "protonmail.com", "fastmail.com"
    };

    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", "temp-mail.org", "yopmail.com",
        "trashmail.com", "dispostable.com", "sharklasers.com"
    };

    public static DomainRisk Classify(string email, IEnumerable<string>? trustedBusinessDomains = null)
    {
        var separator = email.LastIndexOf('@');
        if (separator < 1 || separator == email.Length - 1)
        {
            return DomainRisk.Disposable;
        }

        var domain = email[(separator + 1)..].Trim().ToLowerInvariant();
        if (DisposableDomains.Contains(domain))
        {
            return DomainRisk.Disposable;
        }

        if (FreeDomains.Contains(domain))
        {
            return DomainRisk.Free;
        }

        return trustedBusinessDomains?.Any(candidate =>
            string.Equals(candidate.Trim().TrimStart('@'), domain, StringComparison.OrdinalIgnoreCase)) == true
                ? DomainRisk.Business
                : DomainRisk.Free;
    }
}

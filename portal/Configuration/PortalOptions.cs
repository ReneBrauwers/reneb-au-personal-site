namespace ReneB.Portal.Configuration;

public sealed class PortalOptions
{
    public bool Enabled { get; set; }
    public string DataDirectory { get; set; } = "/app/data";
    public string BackupDirectory { get; set; } = "/app/backups";
    public string CanonicalBaseUrl { get; set; } = "https://reneb.au";
    public string[] AdminEmails { get; set; } = [];
    public string[] TrustedBusinessDomains { get; set; } = [];
    public int AuthRequestsPerMinute { get; set; } = 8;
    public string TrustedProxyNetworks { get; set; } = string.Empty;
}

public sealed class EncryptionOptions
{
    public string KeyFile { get; set; } = "/run/secrets/field-encryption-keyring.json";
    public bool AllowDevelopmentKey { get; set; }
}

public sealed class MailOptions
{
    public string Mode { get; set; } = "Graph";
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string SenderMailbox { get; set; } = string.Empty;
    public string CertificatePath { get; set; } = "/run/secrets/graph-mail-certificate.pem";
    public string PrivateKeyPath { get; set; } = "/run/secrets/graph-mail-private-key.pem";
}

public sealed class CookieKeyProtectionOptions
{
    public string CertificatePath { get; set; } = "/run/secrets/data-protection-certificate.pem";
    public string PrivateKeyPath { get; set; } = "/run/secrets/data-protection-private-key.pem";
}

using System.ComponentModel.DataAnnotations;

namespace ReneB.Portal.Models;

public sealed class PublicCandidateProfile
{
    [Required, StringLength(100)]
    public string CandidateName { get; set; } = "René Brauwers";

    [Required, StringLength(120)]
    public string Headline { get; set; } = "Enterprise architecture leadership for consequential change";

    [Required, StringLength(120)]
    public string CurrentRole { get; set; } = "Head of Enterprise Architecture";

    [Required, StringLength(160)]
    public string CurrentEmployer { get; set; } = "Perpetual Corporate Trust";

    [Required, StringLength(160)]
    public string ProfessionalContext { get; set; } = "Regulated financial services in Sydney, Australia";

    [Required, StringLength(900)]
    public string Summary { get; set; } = string.Empty;

    public List<string> DemonstratedSignals { get; set; } = [];
    public List<string> RolesOfInterest { get; set; } = [];
    public List<string> AreasOfInterest { get; set; } = [];
    public List<string> LocationPreferences { get; set; } = [];
    public List<string> StrongFitSignals { get; set; } = [];
    public List<string> PoorFitSignals { get; set; } = [];

    [Required]
    public DateOnly LastReviewed { get; set; } = new(2026, 8, 12);
}

public sealed class PrivateCandidateProfile
{
    [StringLength(4000)]
    public string? DetailedInterests { get; set; }

    [StringLength(500)]
    public string? PermanentCompensation { get; set; }

    [StringLength(500)]
    public string? ContractCompensation { get; set; }

    [StringLength(1000)]
    public string? Availability { get; set; }

    [StringLength(3000)]
    public string? AdditionalGuidance { get; set; }
}

public sealed record RecruiterRegistration(
    string Name,
    string Email,
    string Organisation,
    string Title,
    string ProfileUrl,
    string Country,
    string? Phone,
    string Purpose);

public enum RecruiterStatus
{
    PendingEmail,
    PendingApproval,
    Active,
    Suspended,
    Deleted
}

public enum DomainRisk
{
    Business,
    Free,
    Disposable
}

public sealed record RecruiterRecord(
    Guid Id,
    string Email,
    string Name,
    string Organisation,
    string Title,
    string ProfileUrl,
    string Country,
    string? Phone,
    string Purpose,
    RecruiterStatus Status,
    DomainRisk DomainRisk,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EmailVerifiedAt,
    DateTimeOffset LastActiveAt);

public sealed record MessageRecord(
    Guid Id,
    Guid RecruiterId,
    string RecruiterName,
    string Organisation,
    string Subject,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record ResumeRecord(
    Guid Id,
    string OriginalFileName,
    string Sha256,
    long Size,
    DateTimeOffset UploadedAt,
    bool IsActive);

public sealed record ResumeGrantRecord(
    Guid RecruiterId,
    Guid ResumeId,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt);

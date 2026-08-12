using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ReneB.Portal.Configuration;
using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Security;

public sealed class RecruiterAccessRequirement : IAuthorizationRequirement;
public sealed class AdminBaseRequirement : IAuthorizationRequirement;
public sealed class AdminTotpRequirement : IAuthorizationRequirement;

public sealed class RecruiterAccessHandler(PortalDatabase database, IOptionsMonitor<PortalOptions> options, TimeProvider time) : AuthorizationHandler<RecruiterAccessRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RecruiterAccessRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            return;
        }
        var recruiter = await database.GetRecruiterAsync(id);
        if (recruiter?.Status != RecruiterStatus.Active)
        {
            return;
        }

        var isAdministrator = options.CurrentValue.AdminEmails
            .Select(PortalDatabase.NormalizeEmail)
            .Contains(PortalDatabase.NormalizeEmail(recruiter.Email), StringComparer.Ordinal);
        if (isAdministrator
            && (!long.TryParse(context.User.FindFirstValue("totp_at"), out var totpAt)
                || time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(totpAt) > TimeSpan.FromHours(8)))
        {
            return;
        }

        context.Succeed(requirement);
    }
}

public sealed class AdminBaseHandler(IOptionsMonitor<PortalOptions> options) : AuthorizationHandler<AdminBaseRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminBaseRequirement requirement)
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email);
        if (email is not null && options.CurrentValue.AdminEmails
            .Select(PortalDatabase.NormalizeEmail)
            .Contains(PortalDatabase.NormalizeEmail(email), StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public sealed class AdminTotpHandler(IOptionsMonitor<PortalOptions> options, TimeProvider time) : AuthorizationHandler<AdminTotpRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminTotpRequirement requirement)
    {
        var email = context.User.FindFirstValue(ClaimTypes.Email);
        if (email is null || !options.CurrentValue.AdminEmails
            .Select(PortalDatabase.NormalizeEmail)
            .Contains(PortalDatabase.NormalizeEmail(email), StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (!long.TryParse(context.User.FindFirstValue("auth_time"), out var authTime)
            || time.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authTime) > TimeSpan.FromHours(8))
        {
            return Task.CompletedTask;
        }

        if (long.TryParse(context.User.FindFirstValue("totp_at"), out _))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

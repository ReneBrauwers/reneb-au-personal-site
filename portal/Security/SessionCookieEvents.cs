using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using ReneB.Portal.Data;

namespace ReneB.Portal.Security;

public sealed class SessionCookieEvents(PortalDatabase database, TimeProvider time) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionToken = context.Principal?.FindFirstValue("session_id");
        var valid = Guid.TryParse(idValue, out var recruiterId)
            && !string.IsNullOrWhiteSpace(sessionToken)
            && await database.ValidateAndTouchSessionAsync(
                recruiterId,
                IdentityService.HashSession(sessionToken),
                time.GetUtcNow().AddMinutes(30),
                context.HttpContext.RequestAborted);

        if (valid)
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}

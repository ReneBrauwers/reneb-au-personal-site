using ReneB.Portal.Data;
using ReneB.Portal.Models;

namespace ReneB.Portal.Services;

public sealed class SiteContentService(PortalDatabase database)
{
    public async Task<SiteSettingsContent> GetSettingsAsync(CancellationToken cancellationToken = default)
        => (await database.GetContentAsync<SiteSettingsContent>(ContentDocumentKeys.SiteSettings, false, cancellationToken)).Content;
}

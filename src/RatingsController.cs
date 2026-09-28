using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MdbListRatings;

/// <summary>A title's scores. Any version of a film answers with its primary version's: scores are fetched per title.</summary>
[ApiController]
[Authorize]
[Route("Ratings")]
public class RatingsController(ILibraryManager libraryManager) : ControllerBase
{
    [HttpGet("{itemId}")]
    public ActionResult<IReadOnlyList<Rating>> Get([FromRoute] Guid itemId)
    {
        var store = Plugin.Instance?.Store;
        if (store is null)
        {
            return Array.Empty<Rating>();
        }

        if (store.Get(itemId) is { } entry)
        {
            return entry.Ratings;
        }

        var primary = libraryManager.GetItemById(itemId) is Video { PrimaryVersionId: { } id } ? store.Get(id) : null;
        return primary?.Ratings ?? [];
    }
}

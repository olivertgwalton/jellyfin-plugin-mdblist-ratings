using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MdbListRatings;

/// <summary>A title's scores, in the order a hero reads them: IMDb, TMDB, Rotten Tomatoes critics, audience.</summary>
[ApiController]
[Authorize]
[Route("Ratings")]
public class RatingsController : ControllerBase
{
    [HttpGet("{itemId}")]
    public ActionResult<IReadOnlyList<Rating>> Get([FromRoute] Guid itemId) =>
        Plugin.Instance?.Store.Get(itemId)?.Ratings ?? [];
}

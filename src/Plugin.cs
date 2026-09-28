using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.MdbListRatings;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>How old a title's scores may get before they are asked for again.</summary>
    public int RefreshDays { get; set; } = 30;
}

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        Store = new RatingStore(Path.Combine(DataFolderPath, "ratings.json"));
    }

    public static Plugin? Instance { get; private set; }

    public RatingStore Store { get; }

    public override string Name => "MDBList Ratings";

    public override Guid Id => Guid.Parse("00132eff-99e1-4ece-a914-d6e0f1dc348d");

    public override string Description =>
        "IMDb, TMDB and Rotten Tomatoes critics and audience scores from MDBList, served at /Ratings/{itemId}.";

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new PluginPageInfo
        {
            // No space in the page name: it becomes a URL. Linked from the sidebar because a
            // sideloaded plugin's own page toasts that the repository has no details for it.
            Name = "MdbListRatings",
            EmbeddedResourcePath = $"{GetType().Namespace}.configPage.html",
            EnableInMainMenu = true,
            DisplayName = Name,
        },
    ];
}

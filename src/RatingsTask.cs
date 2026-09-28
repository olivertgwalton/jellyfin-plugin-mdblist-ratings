using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MdbListRatings;

/// <summary>
/// Asks MDBList for every film and show that has no scores yet, or whose scores are older than
/// <see cref="PluginConfiguration.RefreshDays"/>: after every library scan, as Plex's agents do,
/// and every six hours. Stops at the daily limit and carries on next run.
/// </summary>
public class RatingsTask(ILibraryManager libraryManager, IHttpClientFactory httpClientFactory, ILogger<RatingsTask> logger)
    : IScheduledTask, ILibraryPostScanTask
{
    // Jellyfin makes one instance as a scheduled task and another as a post-scan task.
    private static readonly SemaphoreSlim Running = new(1, 1);

    public string Name => "Fetch MDBList ratings";

    public string Key => "MdbListRatings";

    public string Description => "IMDb, TMDB and Rotten Tomatoes scores for films and shows.";

    public string Category => "Library";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() =>
    [
        new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(6).Ticks },
    ];

    public Task Run(IProgress<double> progress, CancellationToken cancellationToken) =>
        ExecuteAsync(progress, cancellationToken);

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var plugin = Plugin.Instance!;
        var apiKey = plugin.Configuration.ApiKey.Trim();
        if (apiKey.Length == 0)
        {
            logger.LogInformation("No MDBList API key set; skipping");
            return;
        }

        if (!await Running.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await FetchAsync(plugin, apiKey, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            plugin.Store.Save();
            Running.Release();
        }
    }

    private async Task FetchAsync(Plugin plugin, string apiKey, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var store = plugin.Store;
        var items = libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            IsVirtualItem = false,
            Recursive = true,
        });
        store.Retain(items.Select(i => i.Id).ToHashSet());

        var stale = DateTime.UtcNow.AddDays(-plugin.Configuration.RefreshDays);
        var due = items
            .Select(item => (item, fetched: store.Get(item.Id)?.Fetched))
            .Where(x => x.fetched is null || x.fetched < stale)
            .OrderBy(x => x.fetched ?? DateTime.MinValue)
            .Select(x => x.item)
            .ToList();

        var done = 0;
        foreach (var item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Lookup(item) is var (provider, mediaType, id))
            {
                try
                {
                    var ratings = await new MdbListClient(httpClientFactory)
                        .RatingsAsync(provider, mediaType, id, apiKey, cancellationToken).ConfigureAwait(false);
                    store.Set(item.Id, new RatingEntry(DateTime.UtcNow, ratings));
                }
                catch (DailyLimitReachedException)
                {
                    logger.LogInformation("MDBList limit reached after {Done} of {Due}; continuing next run", done, due.Count);
                    return;
                }
            }

            done++;
            if (done % 25 == 0)
            {
                store.Save();
            }

            progress.Report(100.0 * done / due.Count);
        }

        logger.LogInformation("MDBList ratings fetched for {Done} titles", done);
    }

    /// <summary>TMDB first (MDBList's key for both films and shows), then IMDb, then TVDB for a show.</summary>
    private static (string Provider, string MediaType, string Id)? Lookup(BaseItem item)
    {
        var mediaType = item.GetBaseItemKind() == BaseItemKind.Movie ? "movie" : "show";
        foreach (var (provider, key) in (ReadOnlySpan<(string, string)>)[("tmdb", "Tmdb"), ("imdb", "Imdb"), ("tvdb", "Tvdb")])
        {
            if (item.ProviderIds.TryGetValue(key, out var id) && !string.IsNullOrWhiteSpace(id))
            {
                return (provider, mediaType, id);
            }
        }

        return null;
    }
}

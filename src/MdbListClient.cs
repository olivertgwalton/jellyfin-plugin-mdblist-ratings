using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MdbListRatings;

public class DailyLimitReachedException : Exception;

/// <summary>GET https://api.mdblist.com/{provider}/{movie|show}/{id}?apikey=…</summary>
public class MdbListClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <returns>The title's scores; empty where MDBList does not know it.</returns>
    public async Task<List<Rating>> RatingsAsync(
        string provider, string mediaType, string id, string apiKey, CancellationToken cancellationToken)
    {
        var url = $"https://api.mdblist.com/{provider}/{mediaType}/{Uri.EscapeDataString(id)}?apikey={Uri.EscapeDataString(apiKey)}";
        using var response = await httpClientFactory.CreateClient().GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        // MDBList names the limit in `error`: "Daily API limit exceeded!" ends the run; any other
        // is a short window, waited out as its Retry-After says, then the same title asked again.
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var refusal = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (refusal.Contains("Daily", StringComparison.OrdinalIgnoreCase))
            {
                throw new DailyLimitReachedException();
            }

            var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(60);
            await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            return await RatingsAsync(provider, mediaType, id, apiKey, cancellationToken).ConfigureAwait(false);
        }

        // A refused key or a server fault ends the run; the message never carries the URL, which holds the key.
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"MDBList answered {(int)response.StatusCode} for {provider}/{mediaType}/{id}");
        }

        var body = await response.Content.ReadFromJsonAsync<MediaInfo>(Json, cancellationToken).ConfigureAwait(false);
        return Map(body?.Ratings ?? []);
    }

    public static List<Rating> Map(IEnumerable<SourceRating> ratings)
    {
        var mapped = new List<Rating>();
        foreach (var house in (string[])["imdb", "tmdb", "rotten_tomatoes", "rotten_tomatoes_audience"])
        {
            var found = ratings.FirstOrDefault(r => House(r.Source) == house && OutOfTen(r) > 0);
            if (found is not null)
            {
                mapped.Add(new Rating(house, Math.Round(OutOfTen(found)!.Value, 1), found.Votes));
            }
        }

        return mapped;
    }

    private static string? House(string? source) => source switch
    {
        "imdb" => "imdb",
        "tmdb" => "tmdb",
        "tomatoes" => "rotten_tomatoes",
        // The v1 host called the audience score tomatoesaudience; api.mdblist.com calls it popcorn.
        "popcorn" or "tomatoesaudience" => "rotten_tomatoes_audience",
        _ => null,
    };

    /// <summary>MDBList's own `score` is every source rescaled to 100; `value` is each source's own scale.</summary>
    private static double? OutOfTen(SourceRating rating)
    {
        if (rating.Score is { } score)
        {
            return score / 10;
        }

        if (rating.Value is not { } value)
        {
            return null;
        }

        return rating.Source is "tomatoes" or "popcorn" or "tomatoesaudience" || value > 10 ? value / 10 : value;
    }

    public class MediaInfo
    {
        public List<SourceRating>? Ratings { get; set; }
    }

    public class SourceRating
    {
        public string? Source { get; set; }

        public double? Value { get; set; }

        public double? Score { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long? Votes { get; set; }
    }
}

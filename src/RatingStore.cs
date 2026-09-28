using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.MdbListRatings;

/// <summary>One score, named in the vocabulary a client reads: imdb, tmdb, rotten_tomatoes, rotten_tomatoes_audience.</summary>
/// <remarks>Named explicitly: Jellyfin's own API writes PascalCase and clients decode these names.</remarks>
public record Rating(
    [property: JsonPropertyName("House")] string House,
    [property: JsonPropertyName("OutOfTen")] double OutOfTen,
    [property: JsonPropertyName("Votes")] long? Votes);

public record RatingEntry(DateTime Fetched, List<Rating> Ratings);

/// <summary>Every title's scores, by Jellyfin item id, kept as one JSON file in the plugin's data folder.</summary>
public class RatingStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _path;
    private readonly Lock _lock = new();
    private Dictionary<Guid, RatingEntry> _entries;

    public RatingStore(string path)
    {
        _path = path;
        _entries = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<Guid, RatingEntry>>(File.ReadAllText(path), Json) ?? []
            : [];
    }

    public RatingEntry? Get(Guid id)
    {
        lock (_lock)
        {
            return _entries.GetValueOrDefault(id);
        }
    }

    public void Set(Guid id, RatingEntry entry)
    {
        lock (_lock)
        {
            _entries[id] = entry;
        }
    }

    /// <summary>Forgets items no longer in the library.</summary>
    public void Retain(IReadOnlySet<Guid> ids)
    {
        lock (_lock)
        {
            _entries = _entries.Where(e => ids.Contains(e.Key)).ToDictionary();
        }
    }

    public void Save()
    {
        string text;
        lock (_lock)
        {
            text = JsonSerializer.Serialize(_entries, Json);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, _path, overwrite: true);
    }
}

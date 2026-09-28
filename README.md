# MDBList Ratings for Jellyfin

Jellyfin keeps two unnamed scores on a title (`CommunityRating`, `CriticRating`). This plugin
keeps the rest, the way Plex's agents do: **IMDb, TMDB, and Rotten Tomatoes' critics and audience
scores**, fetched from [MDBList](https://mdblist.com) for every film and show, stored on the
server, and served to clients beside the item.

## Install

1. Dashboard ▸ Plugins ▸ Repositories ▸ add
   `https://raw.githubusercontent.com/olivertgwalton/jellyfin-plugin-mdblist-ratings/main/manifest.json`
2. Install **MDBList Ratings** from the catalogue, restart Jellyfin.
3. Paste a free API key from mdblist.com ▸ Preferences into the plugin's settings.

Requires Jellyfin 12.

## What it does

- Fetches after every library scan and every six hours (Dashboard ▸ Scheduled Tasks ▸ *Fetch
  MDBList ratings*), for films and shows that have none yet or whose scores are older than the
  refresh interval (30 days by default). Looked up by TMDB id, then IMDb, then TVDB.
- Waits out MDBList's short rate window; stops at the daily limit (1,000 requests on the free
  tier) and carries on at the next run.
- Keeps everything in `ratings.json` in the plugin's data folder. Items that leave the library
  are forgotten.

## The API (for client authors)

```
GET /Ratings/{itemId}
Authorization: MediaBrowser … Token="…"   (any signed-in user)
```

`200` with the item's scores, in the order a detail page reads them. An item the plugin has no
scores for answers `[]`. **A `404` means the plugin is not installed.**

```json
[
  { "House": "imdb", "OutOfTen": 8.0, "Votes": 1584901 },
  { "House": "tmdb", "OutOfTen": 8.0, "Votes": 39855 },
  { "House": "rotten_tomatoes", "OutOfTen": 9.1, "Votes": 364 },
  { "House": "rotten_tomatoes_audience", "OutOfTen": 9.1, "Votes": 36275 }
]
```

- `House`: `imdb`, `tmdb`, `rotten_tomatoes` (critics, the Tomatometer) or
  `rotten_tomatoes_audience` (the Popcornmeter). Clients should ignore a house they do not know;
  more may be added.
- `OutOfTen`: every score on one scale, one decimal. Rotten Tomatoes' 91% is `9.1`.
- `Votes`: the number of ratings or reviews behind it, where MDBList states one.

Episodes and seasons answer `[]`.

## Build

```
dotnet build src -c Release
```

`./release.sh <version>` builds, zips, publishes a GitHub release and updates `manifest.json`.

Licensed under the GPL-3.0.

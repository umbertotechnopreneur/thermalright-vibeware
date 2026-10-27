/* VBWR B
 *
 * Project: Thermalright VibeWare
 * Repository: https://github.com/umbertotechnopreneur/thermalright-vibeware
 * Creator: Umberto Giacobbi | https://umbertogiacobbi.biz
 *
 * File: OnlineArtistProvider.cs
 * Purpose: Caches bounded public artist facts, images and their attribution.
 *
 * VibeWare: Human intent, AI execution, and plenty of tokens
 * Manifesto: https://umbertogiacobbi.biz/vibeware/manifesto
 *
 * Modified with AI: OpenAI Codex; source documentation and organization; 2026-10-27.
 * Human guidance: Umberto Giacobbi; maintainability and standard file headers.
 *
 * Copyright (c) 2026 Umberto Giacobbi
 * SPDX-License-Identifier: MIT
 *
 * VBWR E */

using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThermalrightVibeWare.Core;

/// <summary>One bounded background lookup; playback never waits for online metadata.</summary>
public sealed class OnlineArtistProvider : IArtistProvider
{
    private readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(10) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly object sync = new();
    private readonly Dictionary<string, (ArtistProfile? Profile, DateTimeOffset At)> cache = [];
    private Task? pending;
    private DateTimeOffset lastMusicBrainzCall;
    private bool disposed;

    /// <summary>Configures bounded public-data requests and local artist caching.</summary>
    public OnlineArtistProvider()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ThermalrightVibeWare/0.1.10 (+https://github.com/umbertotechnopreneur/thermalright-vibeware)");
    }

    /// <summary>Returns cached artist facts and schedules a bounded lookup when needed.</summary>
    /// <param name="media">Current Windows media observation used for artist lookup or artwork.</param>
    public ArtistProfile? Read(NowPlaying media)
    {
        if (string.IsNullOrWhiteSpace(media.Source) || string.IsNullOrWhiteSpace(media.Artist) || media.Artist.Length > 160 || media.Album.Length > 200) { return null; }
        var key = media.Artist + "\n" + media.Album;
        lock (sync)
        {
            if (disposed) { return null; }
            if (cache.TryGetValue(key, out var found) && DateTimeOffset.UtcNow - found.At < TimeSpan.FromHours(found.Profile is null ? 1 : 24)) { return found.Profile; }
            if (pending is null || pending.IsCompleted)
            {
                var artist = media.Artist;
                var album = media.Album;
                pending = Task.Run(async () =>
                {
                    ArtistProfile? profile = null;
                    try { profile = await LookupAsync(artist, album, lifetime.Token); }
                    catch (Exception error) when (error is HttpRequestException or IOException or JsonException or OperationCanceledException or InvalidOperationException or FormatException or ArgumentException) { }
                    lock (sync)
                    {
                        if (disposed) { return; }
                        if (cache.Count >= 16) { cache.Remove(cache.OrderBy(item => item.Value.At).First().Key); }
                        cache[key] = (profile, DateTimeOffset.UtcNow);
                    }
                });
            }
            return null;
        }
    }

    /// <summary>Resolves bounded artist facts from the configured public sources.</summary>
    /// <param name="requestedArtist">Artist text from the selected media session, used as a public-data search term.</param>
    /// <param name="album">Album title used to narrow public artist/album data.</param>
    /// <param name="token">Cancellation signal for the current background operation.</param>
    private async Task<ArtistProfile?> LookupAsync(string requestedArtist, string album, CancellationToken token)
    {
        var query = Uri.EscapeDataString("artist:\"" + EscapeSearch(requestedArtist) + "\"");
        using var search = await JsonAsync("https://musicbrainz.org/ws/2/artist?fmt=json&limit=10&query=" + query, token);
        var matches = Array(search.RootElement, "artists").Where(item => String(item, "name").Equals(requestedArtist.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        // An ambiguous name does not borrow another artist's biography.
        if (matches.Length != 1 || !Guid.TryParse(String(matches[0], "id"), out var artistId)) { return null; }
        using var artist = await JsonAsync($"https://musicbrainz.org/ws/2/artist/{artistId}?fmt=json&inc=url-rels", token);
        var wikidataId = WikidataRelation(artist.RootElement);
        if (wikidataId is null) { return null; }
        using var entity = await JsonAsync($"https://www.wikidata.org/wiki/Special:EntityData/{wikidataId}.json", token);
        var data = entity.RootElement.GetProperty("entities").GetProperty(wikidataId);
        var title = PathString(data, "sitelinks", "enwiki", "title");
        if (title.Length == 0) { return null; }
        var wikipediaUrl = "https://en.wikipedia.org/wiki/" + Uri.EscapeDataString(title.Replace(' ', '_'));
        using var article = await JsonAsync("https://en.wikipedia.org/w/api.php?action=query&format=json&formatversion=2&prop=extracts|pageimages&exintro=1&explaintext=1&exsentences=3&piprop=name&titles=" + Uri.EscapeDataString(title), token);
        var page = Array(article.RootElement.GetProperty("query"), "pages").FirstOrDefault();
        var biography = Plain(String(page, "extract"));
        if (biography.Length == 0) { return null; }
        // One short paragraph, complete sentences when the upstream summary allows it.
        biography = biography.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (biography.Length > 380)
        {
            var end = biography.LastIndexOf('.', 379, 380);
            biography = end > 100 ? biography[..(end + 1)] : biography[..377] + "…";
        }
        byte[]? photo = null;
        string? photoCredit = null, photoSource = null;
        var imageName = String(page, "pageimage");
        if (imageName.Length is > 0 and < 300)
        {
            try { (photo, photoCredit, photoSource) = await PhotoAsync(imageName, token); }
            catch (Exception error) when (!token.IsCancellationRequested && error is HttpRequestException or IOException or JsonException or ArgumentException or InvalidOperationException or OperationCanceledException) { }
        }
        var facts = new List<ArtistFact>();
        var type = String(matches[0], "type");
        var country = String(matches[0], "country");
        if (type.Length > 0) { facts.Add(new("ARTIST", type)); }
        if (country.Length == 2)
        {
            try { facts.Add(new("COUNTRY", new RegionInfo(country).EnglishName)); }
            catch (ArgumentException) { }
        }
        AlbumSales? sales = null;
        if (!string.IsNullOrWhiteSpace(album))
        {
            try { sales = await AlbumSalesAsync(artistId, album, token); }
            catch (Exception error) when (!token.IsCancellationRequested && error is HttpRequestException or IOException or JsonException or InvalidOperationException or FormatException or OperationCanceledException) { }
        }
        return new(requestedArtist, String(matches[0], "name"), biography, wikipediaUrl, "Wikipedia · CC BY-SA 4.0 · excerpt", photo, photoCredit, photoSource, facts, sales);
    }

    /// <summary>Resolves an attributed Wikimedia image and downloads its bounded thumbnail.</summary>
    /// <param name="imageName">Public Wikimedia file title used to resolve an attributed thumbnail.</param>
    /// <param name="token">Cancellation signal for the current background operation.</param>
    private async Task<(byte[]?, string?, string?)> PhotoAsync(string imageName, CancellationToken token)
    {
        using var info = await JsonAsync("https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2&prop=imageinfo&iiprop=url|extmetadata&iiurlwidth=512&titles=" + Uri.EscapeDataString("File:" + imageName), token);
        var page = Array(info.RootElement.GetProperty("query"), "pages").FirstOrDefault();
        var image = Array(page, "imageinfo").FirstOrDefault();
        var credit = Plain(PathString(image, "extmetadata", "Artist", "value"));
        var license = Plain(PathString(image, "extmetadata", "LicenseShortName", "value"));
        if (credit.Length == 0 || credit.Length > 100 || !(license.StartsWith("CC BY", StringComparison.Ordinal) || license is "CC0" or "Public domain")) { return (null, null, null); }
        var url = String(image, "thumburl");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host is not ("upload.wikimedia.org" or "thumb.wikimedia.org")) { return (null, null, null); }
        var bytes = await BytesAsync(uri, 2_000_000, token);
        using var input = new MemoryStream(bytes);
        using var decoded = RasterImage.Open(input);
        var scale = Math.Min(1, 512d / Math.Max(decoded.Width, decoded.Height));
        using var bitmap = new System.Drawing.Bitmap(Math.Max(1, (int)(decoded.Width * scale)), Math.Max(1, (int)(decoded.Height * scale)));
        using (var graphics = System.Drawing.Graphics.FromImage(bitmap)) { graphics.DrawImage(decoded, 0, 0, bitmap.Width, bitmap.Height); }
        using var output = new MemoryStream();
        bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Jpeg);
        return output.Length <= 400_000 ? (output.ToArray(), credit + " · " + license, String(image, "descriptionurl")) : (null, null, null);
    }

    /// <summary>Reads sourced album sales statements when a matching public claim exists.</summary>
    /// <param name="artistId">Resolved MusicBrainz artist identifier.</param>
    /// <param name="album">Album title used to narrow public artist/album data.</param>
    /// <param name="token">Cancellation signal for the current background operation.</param>
    private async Task<AlbumSales?> AlbumSalesAsync(Guid artistId, string album, CancellationToken token)
    {
        var query = Uri.EscapeDataString("releasegroup:\"" + EscapeSearch(album) + "\" AND arid:" + artistId);
        using var search = await JsonAsync("https://musicbrainz.org/ws/2/release-group?fmt=json&limit=10&query=" + query, token);
        var matches = Array(search.RootElement, "release-groups").Where(item => String(item, "title").Equals(album, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length != 1 || !Guid.TryParse(String(matches[0], "id"), out var id)) { return null; }
        using var group = await JsonAsync($"https://musicbrainz.org/ws/2/release-group/{id}?fmt=json&inc=url-rels", token);
        var wikidataId = WikidataRelation(group.RootElement);
        if (wikidataId is null) { return null; }
        using var document = await JsonAsync($"https://www.wikidata.org/wiki/Special:EntityData/{wikidataId}.json", token);
        var entity = document.RootElement.GetProperty("entities").GetProperty(wikidataId);
        if (!entity.TryGetProperty("claims", out var claims)) { return null; }
        foreach (var statement in Array(claims, "P2664"))
        {
            if (String(statement, "rank") == "deprecated" || !statement.TryGetProperty("qualifiers", out var qualifiers)) { continue; }
            // Do not label regional, format-specific or undated counts as worldwide totals.
            if (qualifiers.EnumerateObject().Any(item => item.Name is not ("P585" or "P1001"))) { continue; }
            var scope = PathString(Array(qualifiers, "P1001").FirstOrDefault(), "datavalue", "value", "id");
            var at = PathString(Array(qualifiers, "P585").FirstOrDefault(), "datavalue", "value", "time");
            if (scope != "Q2" || at.Length < 5) { continue; }
            var reference = Array(statement, "references").SelectMany(item => item.TryGetProperty("snaks", out var snaks) ? Array(snaks, "P854") : [])
                .Select(item => PathString(item, "datavalue", "value")).FirstOrDefault(value => Uri.TryCreate(value, UriKind.Absolute, out var link) && link.Scheme == "https");
            if (PathString(statement, "mainsnak", "datavalue", "value", "unit") != "1") { continue; }
            var amount = PathString(statement, "mainsnak", "datavalue", "value", "amount");
            if (reference is null || !double.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out var units) || !double.IsFinite(units) || units < 0) { continue; }
            return new(album, units, "Worldwide units sold", at.TrimStart('+')[..4], reference);
        }
        return null;
    }

    /// <summary>Fetches and parses a bounded JSON response from a public data source.</summary>
    /// <param name="url">Explicit URL to open or query, according to this method's role.</param>
    /// <param name="token">Cancellation signal for the current background operation.</param>
    private async Task<JsonDocument> JsonAsync(string url, CancellationToken token)
    {
        var uri = new Uri(url);
        if (uri.Host == "musicbrainz.org")
        {
            var delay = TimeSpan.FromMilliseconds(1100) - (DateTimeOffset.UtcNow - lastMusicBrainzCall);
            if (delay > TimeSpan.Zero) { await Task.Delay(delay, token); }
            lastMusicBrainzCall = DateTimeOffset.UtcNow;
        }
        return JsonDocument.Parse(await BytesAsync(uri, 2_000_000, token));
    }

    /// <summary>Downloads a response while enforcing the byte limit.</summary>
    /// <param name="uri">Public resource URI to download within the supplied byte limit.</param>
    /// <param name="limit">Maximum permitted response/file length in bytes.</param>
    /// <param name="token">Cancellation signal for the current background operation.</param>
    private async Task<byte[]> BytesAsync(Uri uri, int limit, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        token = timeout.Token;
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > limit) { throw new IOException("Artist response is too large."); }
        await using var input = await response.Content.ReadAsStreamAsync(token);
        using var output = new MemoryStream();
        var buffer = new byte[16384];
        int count;
        while ((count = await input.ReadAsync(buffer, token)) > 0)
        {
            if (output.Length + count > limit) { throw new IOException("Artist response is too large."); }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }

    /// <summary>Extracts the Wikidata identity from a MusicBrainz relation list.</summary>
    /// <param name="entity">JSON entity containing public-data relations.</param>
    private static string? WikidataRelation(JsonElement entity)
    {
        foreach (var relation in Array(entity, "relations"))
        {
            var url = PathString(relation, "url", "resource");
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host is "www.wikidata.org" or "wikidata.org")
            {
                var id = uri.Segments.Last().Trim('/');
                if (Regex.IsMatch(id, "^Q[0-9]+$")) { return id; }
            }
        }
        return null;
    }

    /// <summary>Enumerates a JSON array, treating a missing array as empty.</summary>
    /// <param name="parent">Parent JSON object containing the requested property.</param>
    /// <param name="key">Dictionary key or JSON property name to look up.</param>
    private static IEnumerable<JsonElement> Array(JsonElement parent, string key) => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray() : [];

    /// <summary>Reads one string field from a JSON object.</summary>
    /// <param name="parent">Parent JSON object containing the requested property.</param>
    /// <param name="key">Dictionary key or JSON property name to look up.</param>
    private static string String(JsonElement parent, string key) => PathString(parent, key);

    /// <summary>Follows a JSON property path and returns the available string.</summary>
    /// <param name="value">JSON object from which the property path is traversed.</param>
    /// <param name="path">Ordered JSON property names to traverse.</param>
    private static string PathString(JsonElement value, params string[] path)
    {
        foreach (var key in path) { if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(key, out value)) { return ""; } }
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }

    /// <summary>Removes markup and normalizes whitespace in public source text.</summary>
    /// <param name="text">Text to format, normalize or display in this operation.</param>
    private static string Plain(string text) => Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]*>", "")), @"\s+", " ").Trim();

    /// <summary>Escapes reserved MusicBrainz search characters.</summary>
    /// <param name="value">Media artist/album text to quote for a MusicBrainz search expression.</param>
    private static string EscapeSearch(string value) => Regex.Replace(value, @"([+\-!(){}\[\]^""~*?:\\/])", @"\$1");

    /// <summary>Releases resources owned by this instance.</summary>
    public void Dispose()
    {
        lock (sync) { disposed = true; cache.Clear(); }
        lifetime.Cancel();
        // Do not dispose the CTS while the single background operation still uses it.
        _ = (pending ?? Task.CompletedTask).ContinueWith(_ => { http.Dispose(); lifetime.Dispose(); }, TaskScheduler.Default);
    }
}

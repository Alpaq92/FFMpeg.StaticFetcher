using System.Text.Json.Serialization;

namespace FFMpeg.StaticFetcher.Models;

/// <summary>
/// Small metadata file (<c>.ffmpeg-fetch.json</c>) written alongside the downloaded binaries,
/// recording the release tag they were sourced from so <see cref="Enums.FetchMode.ReFetchIfNewer"/>
/// can tell whether the source has since published a different release.
/// </summary>
internal record FetchManifest
{
    [JsonPropertyName("tag")]
    public string? Tag { get; init; }
}

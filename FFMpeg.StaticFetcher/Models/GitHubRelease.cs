using System.Text.Json.Serialization;

namespace FFMpeg.StaticFetcher.Models;

internal record GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; init; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; init; }
}

internal record GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; init; }
}

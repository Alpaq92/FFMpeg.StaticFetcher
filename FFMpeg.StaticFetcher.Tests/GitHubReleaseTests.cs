using System.Text.Json;
using FFMpeg.StaticFetcher.Models;

namespace FFMpeg.StaticFetcher.Tests;

public class GitHubReleaseTests
{
    [Fact]
    public void Deserialize_ValidJson_MapsAllProperties()
    {
        const string json = """
        {
            "tag_name": "b6.1.1",
            "assets": [
                {
                    "name": "ffmpeg-win32-x64.gz",
                    "browser_download_url": "https://example.com/ffmpeg-win32-x64.gz",
                    "size": 12345
                },
                {
                    "name": "ffprobe-win32-x64.gz",
                    "browser_download_url": "https://example.com/ffprobe-win32-x64.gz",
                    "size": 67890
                }
            ]
        }
        """;

        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        Assert.NotNull(release);
        Assert.Equal("b6.1.1", release.TagName);
        Assert.NotNull(release.Assets);
        Assert.Equal(2, release.Assets.Count);

        Assert.Equal("ffmpeg-win32-x64.gz", release.Assets[0].Name);
        Assert.Equal("https://example.com/ffmpeg-win32-x64.gz", release.Assets[0].BrowserDownloadUrl);

        Assert.Equal("ffprobe-win32-x64.gz", release.Assets[1].Name);
        Assert.Equal("https://example.com/ffprobe-win32-x64.gz", release.Assets[1].BrowserDownloadUrl);
    }

    [Fact]
    public void Deserialize_MinimalJson_DefaultsToNulls()
    {
        const string json = "{}";

        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        Assert.NotNull(release);
        Assert.Null(release.TagName);
        Assert.Null(release.Assets);
    }

    [Fact]
    public void Deserialize_EmptyAssets_ReturnsEmptyList()
    {
        const string json = """
        {
            "tag_name": "v1.0",
            "assets": []
        }
        """;

        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        Assert.NotNull(release);
        Assert.NotNull(release.Assets);
        Assert.Empty(release.Assets);
    }

    [Fact]
    public void Deserialize_AssetWithNullFields_DefaultsCorrectly()
    {
        const string json = """
        {
            "assets": [{}]
        }
        """;

        var release = JsonSerializer.Deserialize<GitHubRelease>(json);

        Assert.NotNull(release);
        Assert.NotNull(release.Assets);
        Assert.Single(release.Assets);
        Assert.Null(release.Assets[0].Name);
        Assert.Null(release.Assets[0].BrowserDownloadUrl);
    }
}

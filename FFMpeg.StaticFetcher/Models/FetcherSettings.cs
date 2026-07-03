using FFMpeg.StaticFetcher.Enums;

namespace FFMpeg.StaticFetcher.Models;

public record FetcherSettings
{
    /// <summary>
    /// Override the source GitHub repository in <c>owner/repo</c> form (e.g. a fork of
    /// <c>eugeneware/ffmpeg-static</c> that follows the same release/asset naming).
    /// When <c>null</c>, the default <c>eugeneware/ffmpeg-static</c> is used.
    /// </summary>
    public string? Repository { get; init; }

    /// <summary>
    /// Direct URL overrides per platform. When an entry exists for the resolved platform,
    /// its URLs are used instead of the GitHub release catalog — even when <c>Repository</c>
    /// is also set. Per-binary URLs that are <c>null</c> fall back to the release catalog,
    /// so a partial override (e.g. only <c>FFMpegUrl</c>) is valid.
    /// </summary>
    /// <remarks>
    /// The archive format is auto-detected from the downloaded content (gzip, xz, bzip2, zip, 7z, rar,
    /// tar.gz, tar.xz, tar.bz2, plain tar, or raw binary). No format hint is required.
    /// </remarks>
    public IReadOnlyDictionary<SupportedPlatform, PlatformSource>? SourceOverrides { get; init; }

    /// <summary>
    /// Optional GitHub token sent as a <c>Bearer</c> Authorization header on <c>api.github.com</c> release-metadata
    /// requests only — never on asset or <see cref="SourceOverrides"/> downloads. Raises GitHub's anonymous
    /// 60 requests/hour limit. When <c>null</c>, the <c>GITHUB_TOKEN</c> environment variable is used if present.
    /// </summary>
    public string? GitHubToken { get; init; }

    /// <summary>
    /// Maximum number of bytes read from a single download or written while decompressing/extracting a single binary,
    /// guarding against a hostile mirror serving a decompression bomb. Exceeding it throws. Defaults to 2 GiB when <c>null</c>.
    /// </summary>
    public long? MaxDownloadBytes { get; init; }
}

public record PlatformSource
{
    /// <summary>Direct URL for the <c>ffmpeg</c> binary or archive containing it. <c>null</c> falls back to the release catalog.</summary>
    public Uri? FFMpegUrl { get; init; }

    /// <summary>Direct URL for the <c>ffprobe</c> binary or archive containing it. <c>null</c> falls back to the release catalog.</summary>
    public Uri? FFProbeUrl { get; init; }
}

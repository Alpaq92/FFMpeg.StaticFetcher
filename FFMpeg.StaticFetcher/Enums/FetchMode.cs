namespace FFMpeg.StaticFetcher.Enums;

/// <summary>
/// Controls what happens to binaries that are already present in the output folder.
/// </summary>
public enum FetchMode
{
    /// <summary>
    /// Reuse binaries already present in the output folder and only download the missing ones.
    /// No network call is made when nothing is missing. This is the default.
    /// </summary>
    ReuseExisting,

    /// <summary>
    /// Reuse binaries already on disk only while they match the source's current release.
    /// When the source's resolved release tag differs from the tag recorded in the output folder
    /// (<c>.ffmpeg-fetch.json</c>), the binaries are re-downloaded. Because the GitHub <c>latest</c>
    /// endpoint always returns the newest release, a differing tag means a newer build is available.
    /// Binaries resolved from a direct-URL override carry no release tag and are always reused in this mode.
    /// </summary>
    ReFetchIfNewer,

    /// <summary>
    /// Delete the output folder and re-download everything, regardless of what is already on disk.
    /// </summary>
    Reload
}

namespace FFMpeg.StaticFetcher.Models;

/// <summary>
/// Resolved absolute paths to the requested binaries. A property is <c>null</c> when that binary
/// was not part of the requested <see cref="Enums.FFMpegBinary"/> flags.
/// </summary>
public record FFMpegPaths
{
    /// <summary>Absolute path to the <c>ffmpeg</c> executable, or <c>null</c> if it was not requested.</summary>
    public string? FFMpegPath { get; init; }

    /// <summary>Absolute path to the <c>ffprobe</c> executable, or <c>null</c> if it was not requested.</summary>
    public string? FFProbePath { get; init; }
}

namespace FFMpeg.StaticFetcher.Enums;

/// <summary>
/// Which binaries to fetch. A <c>[Flags]</c> enum — combine with <c>|</c> to request both.
/// </summary>
[Flags]
public enum FFMpegBinary : byte
{
    /// <summary>The <c>ffmpeg</c> encoder/decoder binary.</summary>
    FFMpeg = 1,

    /// <summary>The <c>ffprobe</c> media-inspection binary.</summary>
    FFProbe = 2
}

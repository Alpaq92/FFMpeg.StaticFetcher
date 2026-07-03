using System.Runtime.InteropServices;

namespace FFMpeg.StaticFetcher.Platform;

internal interface IRuntimeProbe
{
    bool IsOsPlatform(OSPlatform platform);
    Architecture Architecture { get; }
}

internal sealed class DefaultRuntimeProbe : IRuntimeProbe
{
    public static readonly DefaultRuntimeProbe Instance = new();

    public bool IsOsPlatform(OSPlatform platform) => RuntimeInformation.IsOSPlatform(platform);
    public Architecture Architecture => RuntimeInformation.OSArchitecture;
}

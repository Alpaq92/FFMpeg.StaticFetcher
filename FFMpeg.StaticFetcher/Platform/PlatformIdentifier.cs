using System.Runtime.InteropServices;
using FFMpeg.StaticFetcher.Enums;

namespace FFMpeg.StaticFetcher.Platform;

internal sealed record PlatformAsset
{
    public required SupportedPlatform Platform { get; init; }
    public required string Os { get; init; }
    public required string Arch { get; init; }
    public bool IsWindows => Os == PlatformIdentifier.WindowsOs;
}

internal static class PlatformIdentifier
{
    internal const string WindowsOs = "win32";
    private const string LinuxOs    = "linux";
    private const string DarwinOs   = "darwin";

    private static readonly Dictionary<SupportedPlatform, (string Os, string Arch)> Map = new()
    {
        [SupportedPlatform.Windows64]  = (WindowsOs, "x64"),
        [SupportedPlatform.Linux64]    = (LinuxOs,   "x64"),
        [SupportedPlatform.Linux32]    = (LinuxOs,   "ia32"),
        [SupportedPlatform.LinuxArm]   = (LinuxOs,   "arm"),
        [SupportedPlatform.LinuxArm64] = (LinuxOs,   "arm64"),
        [SupportedPlatform.Osx64]      = (DarwinOs,  "x64"),
        [SupportedPlatform.OsxArm64]   = (DarwinOs,  "arm64")
    };

    public static PlatformAsset Resolve(SupportedPlatform? platformOverride = null)
        => Resolve(platformOverride, DefaultRuntimeProbe.Instance);

    internal static PlatformAsset Resolve(SupportedPlatform? platformOverride, IRuntimeProbe probe)
    {
        var platform = platformOverride ?? DetectRuntimePlatform(probe);

        if (!Map.TryGetValue(platform, out var entry))
            throw new ArgumentOutOfRangeException(nameof(platformOverride), platform, null);

        return new PlatformAsset { Platform = platform, Os = entry.Os, Arch = entry.Arch };
    }

    internal static SupportedPlatform DetectRuntimePlatform(IRuntimeProbe probe)
    {
        if (probe.IsOsPlatform(OSPlatform.Windows))
        {
            return probe.Architecture switch
            {
                Architecture.X64 => SupportedPlatform.Windows64,
                // no native arm64 build; use x64 under emulation
                Architecture.Arm64 => SupportedPlatform.Windows64,
                _ => throw new PlatformNotSupportedException(
                    $"Windows {probe.Architecture} is not supported by eugeneware/ffmpeg-static")
            };
        }

        if (probe.IsOsPlatform(OSPlatform.Linux))
        {
            return probe.Architecture switch
            {
                Architecture.X64 => SupportedPlatform.Linux64,
                Architecture.X86 => SupportedPlatform.Linux32,
                Architecture.Arm => SupportedPlatform.LinuxArm,
                Architecture.Arm64 => SupportedPlatform.LinuxArm64,
                _ => throw new PlatformNotSupportedException(
                    $"Linux {probe.Architecture} is not supported by eugeneware/ffmpeg-static")
            };
        }

        if (probe.IsOsPlatform(OSPlatform.OSX))
        {
            return probe.Architecture switch
            {
                Architecture.X64 => SupportedPlatform.Osx64,
                Architecture.Arm64 => SupportedPlatform.OsxArm64,
                _ => throw new PlatformNotSupportedException(
                    $"macOS {probe.Architecture} is not supported by eugeneware/ffmpeg-static")
            };
        }

        throw new PlatformNotSupportedException("Unsupported OS platform");
    }
}

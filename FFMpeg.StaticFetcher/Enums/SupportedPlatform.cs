namespace FFMpeg.StaticFetcher.Enums;

/// <summary>
/// A platform (OS + architecture) for which static binaries are published. Pass one to force a
/// specific target instead of auto-detecting the current runtime.
/// </summary>
public enum SupportedPlatform : byte
{
    /// <summary>Windows, x86-64.</summary>
    Windows64,

    /// <summary>Linux, x86-64.</summary>
    Linux64,

    /// <summary>Linux, x86 (32-bit).</summary>
    Linux32,

    /// <summary>Linux, ARMv7 (armhf).</summary>
    LinuxArm,

    /// <summary>Linux, ARM64 (aarch64).</summary>
    LinuxArm64,

    /// <summary>macOS, x86-64 (Intel).</summary>
    Osx64,

    /// <summary>macOS, ARM64 (Apple Silicon).</summary>
    OsxArm64
}

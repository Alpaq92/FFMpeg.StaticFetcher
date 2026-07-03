using System.Runtime.InteropServices;
using FFMpeg.StaticFetcher.Enums;
using FFMpeg.StaticFetcher.Platform;

namespace FFMpeg.StaticFetcher.Tests;

public class PlatformIdentifierTests
{
    private sealed class FakeProbe(OSPlatform os, Architecture arch) : IRuntimeProbe
    {
        public bool IsOsPlatform(OSPlatform platform) => platform == os;
        public Architecture Architecture { get; } = arch;
    }

    [Theory]
    [InlineData(SupportedPlatform.Windows64, "win32", "x64")]
    [InlineData(SupportedPlatform.Linux64, "linux", "x64")]
    [InlineData(SupportedPlatform.Linux32, "linux", "ia32")]
    [InlineData(SupportedPlatform.LinuxArm, "linux", "arm")]
    [InlineData(SupportedPlatform.LinuxArm64, "linux", "arm64")]
    [InlineData(SupportedPlatform.Osx64, "darwin", "x64")]
    [InlineData(SupportedPlatform.OsxArm64, "darwin", "arm64")]
    public void Resolve_WithOverride_ReturnsExpectedOsAndArch(SupportedPlatform platform, string expectedOs, string expectedArch)
    {
        var asset = PlatformIdentifier.Resolve(platform);

        Assert.Equal(platform, asset.Platform);
        Assert.Equal(expectedOs, asset.Os);
        Assert.Equal(expectedArch, asset.Arch);
    }

    [Fact]
    public void Resolve_WithoutOverride_ReturnsNonEmptyValues()
    {
        var asset = PlatformIdentifier.Resolve();

        Assert.False(string.IsNullOrWhiteSpace(asset.Os));
        Assert.False(string.IsNullOrWhiteSpace(asset.Arch));
    }

    [Fact]
    public void Resolve_WithInvalidEnum_ThrowsArgumentOutOfRangeException()
    {
        var invalid = (SupportedPlatform)99;

        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformIdentifier.Resolve(invalid));
    }

    [Theory]
    [InlineData(Architecture.X64, SupportedPlatform.Windows64)]
    [InlineData(Architecture.Arm64, SupportedPlatform.Windows64)]
    public void DetectRuntimePlatform_Windows_ResolvesArch(Architecture arch, SupportedPlatform expected)
    {
        var probe = new FakeProbe(OSPlatform.Windows, arch);

        Assert.Equal(expected, PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Theory]
    [InlineData(Architecture.X64, SupportedPlatform.Linux64)]
    [InlineData(Architecture.X86, SupportedPlatform.Linux32)]
    [InlineData(Architecture.Arm, SupportedPlatform.LinuxArm)]
    [InlineData(Architecture.Arm64, SupportedPlatform.LinuxArm64)]
    public void DetectRuntimePlatform_Linux_ResolvesArch(Architecture arch, SupportedPlatform expected)
    {
        var probe = new FakeProbe(OSPlatform.Linux, arch);

        Assert.Equal(expected, PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Theory]
    [InlineData(Architecture.X64, SupportedPlatform.Osx64)]
    [InlineData(Architecture.Arm64, SupportedPlatform.OsxArm64)]
    public void DetectRuntimePlatform_Osx_ResolvesArch(Architecture arch, SupportedPlatform expected)
    {
        var probe = new FakeProbe(OSPlatform.OSX, arch);

        Assert.Equal(expected, PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Theory]
    [InlineData(Architecture.X86)] // Windows x86 not supported
    [InlineData(Architecture.Arm)] // Windows arm not supported
    public void DetectRuntimePlatform_Windows_UnsupportedArch_Throws(Architecture arch)
    {
        var probe = new FakeProbe(OSPlatform.Windows, arch);

        Assert.Throws<PlatformNotSupportedException>(() => PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Fact]
    public void DetectRuntimePlatform_Linux_UnsupportedArch_Throws()
    {
        var probe = new FakeProbe(OSPlatform.Linux, Architecture.Wasm);

        Assert.Throws<PlatformNotSupportedException>(() => PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Theory]
    [InlineData(Architecture.X86)]
    [InlineData(Architecture.Arm)]
    public void DetectRuntimePlatform_Osx_UnsupportedArch_Throws(Architecture arch)
    {
        var probe = new FakeProbe(OSPlatform.OSX, arch);

        Assert.Throws<PlatformNotSupportedException>(() => PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Fact]
    public void DetectRuntimePlatform_UnknownOs_Throws()
    {
        var probe = new FakeProbe(OSPlatform.Create("FreeBSD"), Architecture.X64);

        Assert.Throws<PlatformNotSupportedException>(() => PlatformIdentifier.DetectRuntimePlatform(probe));
    }

    [Fact]
    public void DefaultRuntimeProbe_DelegatesToRuntimeInformation()
    {
        var probe = DefaultRuntimeProbe.Instance;

        // At least one of the three major platforms must be true — proves the delegation runs.
        var anyOs = probe.IsOsPlatform(OSPlatform.Windows)
                 || probe.IsOsPlatform(OSPlatform.Linux)
                 || probe.IsOsPlatform(OSPlatform.OSX);

        Assert.True(anyOs);
        Assert.Equal(RuntimeInformation.OSArchitecture, probe.Architecture);
    }
}

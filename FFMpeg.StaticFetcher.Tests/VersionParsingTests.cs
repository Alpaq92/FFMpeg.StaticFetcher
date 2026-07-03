namespace FFMpeg.StaticFetcher.Tests;

public class VersionParsingTests
{
    [Theory]
    // Real first-line banners from the vendors eugeneware/ffmpeg-static repackages.
    [InlineData("ffmpeg version 6.1.1-full_build-www.gyan.dev Copyright (c) 2000-2023", "6.1.1")]
    [InlineData("ffmpeg version 6.1-static https://johnvansickle.com/ffmpeg/", "6.1")]
    [InlineData("ffmpeg version 6.1.1 Copyright (c) 2000-2023 the FFmpeg developers", "6.1.1")]
    [InlineData("ffmpeg version 4.4.1-0ubuntu1 Copyright", "4.4.1")]
    public void ParseVersionToken_ParsesVendorBanners(string banner, string expected)
    {
        var parsed = FFMpegStaticFetcher.ParseVersionToken(banner);

        Assert.Equal(Version.Parse(expected), parsed);
    }

    [Theory]
    // Git/nightly builds and junk have no semantic version → null.
    [InlineData("ffmpeg version N-113802-g3f1c8b2a9 Copyright")]
    [InlineData("ffmpeg version git-2024-01-01 Copyright")]
    [InlineData("not an ffmpeg banner at all")]
    [InlineData("")]
    public void ParseVersionToken_ReturnsNullWhenNoSemver(string banner)
    {
        Assert.Null(FFMpegStaticFetcher.ParseVersionToken(banner));
    }

    [Theory]
    [InlineData("b6.1.1", "6.1.1")]
    [InlineData("b6.0", "6.0")]
    [InlineData("v1.0", "1.0")]
    [InlineData("4.3.1", "4.3.1")]
    public void ParseTagVersion_StripsVendorPrefix(string tag, string expected)
    {
        Assert.Equal(Version.Parse(expected), FFMpegStaticFetcher.ParseTagVersion(tag));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void ParseTagVersion_ReturnsNullWhenNoNumber(string? tag)
    {
        Assert.Null(FFMpegStaticFetcher.ParseTagVersion(tag));
    }

    [Fact]
    public void ParsedVersions_CompareAcrossComponentCounts()
    {
        // The comparison the policy relies on: a more-specific local build counts as newer than a coarser tag.
        Assert.True(FFMpegStaticFetcher.ParseVersionToken("ffmpeg version 6.1.1")! >
                    FFMpegStaticFetcher.ParseTagVersion("b6.1"));

        Assert.True(FFMpegStaticFetcher.ParseVersionToken("ffmpeg version 6.0")! <
                    FFMpegStaticFetcher.ParseTagVersion("b6.1.1"));
    }
}

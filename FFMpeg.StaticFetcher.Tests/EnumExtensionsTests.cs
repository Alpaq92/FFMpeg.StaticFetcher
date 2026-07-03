using FFMpeg.StaticFetcher.Enums;
using FFMpeg.StaticFetcher.Extensions;

namespace FFMpeg.StaticFetcher.Tests;

public class EnumExtensionsTests
{
    [Fact]
    public void GetFlags_BothFlags_ReturnsBothValues()
    {
        var combined = FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe;

        var flags = combined.GetFlags();

        Assert.Equal(2, flags.Length);
        Assert.Contains(FFMpegBinary.FFMpeg, flags);
        Assert.Contains(FFMpegBinary.FFProbe, flags);
    }

    [Fact]
    public void GetFlags_SingleFlag_ReturnsSingleValue()
    {
        var flags = FFMpegBinary.FFMpeg.GetFlags();

        Assert.Single(flags);
        Assert.Contains(FFMpegBinary.FFMpeg, flags);
    }

    [Fact]
    public void GetFlags_FFProbeOnly_ReturnsSingleValue()
    {
        var flags = FFMpegBinary.FFProbe.GetFlags();

        Assert.Single(flags);
        Assert.Contains(FFMpegBinary.FFProbe, flags);
    }
}

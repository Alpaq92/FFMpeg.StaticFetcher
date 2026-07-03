using FFMpeg.StaticFetcher.Exceptions;

namespace FFMpeg.StaticFetcher.Tests;

public class FFMpegStaticFetcherExceptionTests
{
    [Fact]
    public void Constructor_MessageOnly_SetsMessageAndDefaultDetail()
    {
        var ex = new FFMpegStaticFetcherException("something went wrong");

        Assert.Equal("something went wrong", ex.Message);
        Assert.Equal("", ex.Detail);
    }

    [Fact]
    public void Constructor_MessageAndDetail_SetsBothProperties()
    {
        var ex = new FFMpegStaticFetcherException("something went wrong", "extra context");

        Assert.Equal("something went wrong", ex.Message);
        Assert.Equal("extra context", ex.Detail);
    }

    [Fact]
    public void Constructor_MessageAndInnerException_SetsMessageAndInner()
    {
        var inner = new InvalidOperationException("boom");
        var ex = new FFMpegStaticFetcherException("outer", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Equal("", ex.Detail);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void Constructor_MessageDetailAndInnerException_SetsAllThree()
    {
        var inner = new InvalidOperationException("root cause");
        var ex = new FFMpegStaticFetcherException("outer", "extra context", inner);

        Assert.Equal("outer", ex.Message);
        Assert.Equal("extra context", ex.Detail);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void IsException()
    {
        var ex = new FFMpegStaticFetcherException("test");

        Assert.IsAssignableFrom<Exception>(ex);
    }
}

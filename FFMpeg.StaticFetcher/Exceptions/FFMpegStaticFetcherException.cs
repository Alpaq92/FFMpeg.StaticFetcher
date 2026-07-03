namespace FFMpeg.StaticFetcher.Exceptions;

public class FFMpegStaticFetcherException : Exception
{
    public string Detail { get; } = "";

    public FFMpegStaticFetcherException(string message) : base(message) { }

    public FFMpegStaticFetcherException(string message, string detail) : base(message)
    {
        Detail = detail;
    }

    public FFMpegStaticFetcherException(string message, Exception innerException) : base(message, innerException) { }

    public FFMpegStaticFetcherException(string message, string detail, Exception innerException) : base(message, innerException)
    {
        Detail = detail;
    }
}

namespace FFMpeg.StaticFetcher.Tests;

public abstract class TempFolderTestBase : IDisposable
{
    protected readonly string _outputFolder = Path.Combine(Path.GetTempPath(), $"ffmpeg_test_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputFolder))
            Directory.Delete(_outputFolder, recursive: true);

        GC.SuppressFinalize(this);
    }
}

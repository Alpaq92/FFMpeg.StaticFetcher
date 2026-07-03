using FFMpeg.StaticFetcher.Enums;
using FFMpeg.StaticFetcher.Exceptions;

namespace FFMpeg.StaticFetcher.Tests;

// Live end-to-end tests that hit api.github.com and download real binaries. Excluded from the default
// CI lane (which runs `--filter "Category!=Integration"`) because the unauthenticated GitHub API is
// rate-limited and the downloads are slow; run them explicitly with `--filter "Category=Integration"`.
[Trait("Category", "Integration")]
public class FFMpegStaticFetcherTests : TempFolderTestBase
{
    [Fact]
    public async Task DownloadBinaries_FFMpegOnly_DownloadsSingleBinary()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder);

        Assert.NotNull(result.FFMpegPath);
        Assert.Null(result.FFProbePath);
        Assert.True(File.Exists(result.FFMpegPath));
        Assert.Equal("ffmpeg", Path.GetFileNameWithoutExtension(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinaries_FFProbeOnly_DownloadsSingleBinary()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFProbe,
            outputFolder: _outputFolder);

        Assert.NotNull(result.FFProbePath);
        Assert.Null(result.FFMpegPath);
        Assert.True(File.Exists(result.FFProbePath));
        Assert.Equal("ffprobe", Path.GetFileNameWithoutExtension(result.FFProbePath));
    }

    [Fact]
    public async Task DownloadBinaries_Both_DownloadsTwoBinaries()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            outputFolder: _outputFolder);

        Assert.NotNull(result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
        Assert.True(File.Exists(result.FFMpegPath));
        Assert.True(File.Exists(result.FFProbePath));
        Assert.Equal("ffmpeg", Path.GetFileNameWithoutExtension(result.FFMpegPath));
        Assert.Equal("ffprobe", Path.GetFileNameWithoutExtension(result.FFProbePath));
    }

    [Fact]
    public async Task DownloadBinaries_SpecificVersion_Downloads()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            version: "b6.1.1");

        Assert.NotNull(result.FFMpegPath);
        Assert.True(File.Exists(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinaries_WithPlatformOverride_Downloads()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Windows64);

        Assert.NotNull(result.FFMpegPath);
        Assert.True(File.Exists(result.FFMpegPath));
        Assert.Equal("ffmpeg.exe", Path.GetFileName(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinaries_InvalidVersion_ThrowsWithDetail()
    {
        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinaries(
                outputFolder: _outputFolder,
                version: "nonexistent-version-tag"));

        Assert.Contains("nonexistent-version-tag", ex.Message);
        Assert.False(string.IsNullOrWhiteSpace(ex.Detail));
    }

    [Fact]
    public async Task DownloadBinaries_FilesAreNonEmpty()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            outputFolder: _outputFolder);

        Assert.True(new FileInfo(result.FFMpegPath!).Length > 0);
        Assert.True(new FileInfo(result.FFProbePath!).Length > 0);
    }

    [Fact]
    public async Task DownloadBinaries_CreatesOutputFolder()
    {
        Assert.False(Directory.Exists(_outputFolder));

        await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder);

        Assert.True(Directory.Exists(_outputFolder));
    }

    [Fact]
    public async Task DownloadBinaries_Reload_CleansAndRedownloads()
    {
        // First download
        await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder);

        // Place a dummy file to verify the folder gets wiped
        var dummyFile = Path.Combine(_outputFolder, "should_be_deleted.txt");
        await File.WriteAllTextAsync(dummyFile, "dummy");

        // Reload
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            mode: FetchMode.Reload);

        Assert.NotNull(result.FFMpegPath);
        Assert.True(File.Exists(result.FFMpegPath));
        Assert.False(File.Exists(dummyFile), "Dummy file should have been deleted when mode: FetchMode.Reload");
    }

    [Fact]
    public async Task DownloadBinaries_Reload_Both_DownloadsTwoBinaries()
    {
        var result = await FFMpegStaticFetcher.DownloadBinaries(
            outputFolder: _outputFolder,
            mode: FetchMode.Reload);

        Assert.NotNull(result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
        Assert.Equal("ffmpeg", Path.GetFileNameWithoutExtension(result.FFMpegPath));
        Assert.Equal("ffprobe", Path.GetFileNameWithoutExtension(result.FFProbePath));
    }

    [Fact]
    public async Task DownloadBinaries_Reload_WorksWhenFolderDoesNotExist()
    {
        Assert.False(Directory.Exists(_outputFolder));

        var result = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            mode: FetchMode.Reload);

        Assert.NotNull(result.FFMpegPath);
        Assert.True(File.Exists(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinaries_UseExisting_ReusesBinariesOnDisk()
    {
        // First download
        var first = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder);

        var originalTimestamp = File.GetLastWriteTimeUtc(first.FFMpegPath!);

        // Wait a moment to distinguish timestamps
        await Task.Delay(50);

        // Second call without reload should reuse
        var second = await FFMpegStaticFetcher.DownloadBinaries(
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder);

        Assert.Equal(first.FFMpegPath, second.FFMpegPath);
        Assert.Equal(originalTimestamp, File.GetLastWriteTimeUtc(second.FFMpegPath!));
    }
}

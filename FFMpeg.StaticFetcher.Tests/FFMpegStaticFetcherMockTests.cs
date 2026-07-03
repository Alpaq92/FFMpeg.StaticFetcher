using System.IO.Compression;
using System.Net;
using System.Text;
using FFMpeg.StaticFetcher.Enums;
using FFMpeg.StaticFetcher.Exceptions;
using FFMpeg.StaticFetcher.Models;

namespace FFMpeg.StaticFetcher.Tests;

public class FFMpegStaticFetcherMockTests : TempFolderTestBase
{
    private static byte[] CreateGzipContent(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write(data);
        }
        return ms.ToArray();
    }

    private static HttpClient CreateMockClient(string releaseJson, byte[]? binaryContent = null)
    {
        binaryContent ??= CreateGzipContent([0xDE, 0xAD]);

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();

            if (url.Contains("/releases/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(releaseJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(binaryContent)
            };
        });

        return new HttpClient(handler);
    }

    private static string BuildReleaseJson(string tagName, params (string name, string url)[] assets)
    {
        var assetsJson = string.Join(",", assets.Select(a =>
            $"{{\"name\":\"{a.name}\",\"browser_download_url\":\"{a.url}\",\"size\":1234}}"));

        return $"{{\"tag_name\":\"{tagName}\",\"assets\":[{assetsJson}]}}";
    }

    [Fact]
    public async Task DownloadBinariesCore_LinuxPlatform_NoExeExtension()
    {
        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));

        using var client = CreateMockClient(json);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.NotNull(result.FFMpegPath);
        Assert.Null(result.FFProbePath);
        Assert.Equal("ffmpeg", Path.GetFileName(result.FFMpegPath));
        Assert.True(File.Exists(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_WindowsPlatform_HasExeExtension()
    {
        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-win32-x64.gz", "https://mock/ffmpeg-win32-x64.gz"));

        using var client = CreateMockClient(json);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Windows64,
            version: null);

        Assert.NotNull(result.FFMpegPath);
        Assert.Equal("ffmpeg.exe", Path.GetFileName(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_AssetNotFound_ThrowsWithAvailableAssets()
    {
        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-darwin-x64.gz", "https://mock/ffmpeg-darwin-x64.gz"));

        using var client = CreateMockClient(json);

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null));

        Assert.Contains("ffmpeg-linux-x64.gz", ex.Message);
        Assert.Contains("not found", ex.Message);
        Assert.Contains("ffmpeg-darwin-x64.gz", ex.Detail);
    }

    [Fact]
    public async Task DownloadBinariesCore_AssetNotFound_NullAssets_Throws()
    {
        const string json = "{\"tag_name\":\"v1.0\"}";

        using var client = CreateMockClient(json);

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task DownloadBinariesCore_HttpFailure_ThrowsWithStatusAndBody()
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{\"message\":\"Not Found\"}")
            });

        using var client = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null));

        Assert.Contains("404", ex.Message);
        Assert.Contains("Not Found", ex.Detail);
    }

    [Fact]
    public async Task DownloadBinariesCore_DeserializationReturnsNull_Throws()
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            });

        using var client = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null));

        Assert.Contains("deserialize", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadBinariesCore_BothBinaries_DownloadsTwoFiles()
    {
        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"),
            ("ffprobe-linux-x64.gz", "https://mock/ffprobe-linux-x64.gz"));

        using var client = CreateMockClient(json);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.NotNull(result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
        Assert.Equal("ffmpeg", Path.GetFileName(result.FFMpegPath));
        Assert.Equal("ffprobe", Path.GetFileName(result.FFProbePath));
    }

    [Fact]
    public async Task DownloadBinariesCore_SpecificVersion_UsesVersionUrl()
    {
        string? capturedUrl = null;
        var gzContent = CreateGzipContent([0x01]);

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();

            if (url.Contains("/releases/"))
            {
                capturedUrl = url;
                var json = BuildReleaseJson("b6.1.1",
                    ("ffmpeg-win32-x64.gz", "https://mock/ffmpeg-win32-x64.gz"));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Windows64,
            version: "b6.1.1");

        Assert.NotNull(capturedUrl);
        Assert.Contains("tags/b6.1.1", capturedUrl);
    }


    [Fact]
    public async Task DownloadBinariesCore_UseExisting_SkipsHttpWhenFilePresent()
    {
        // Pre-create the target file so the fetcher should reuse it.
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, [0x01, 0x02, 0x03]);
        var originalBytes = await File.ReadAllBytesAsync(existing);

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.Equal(0, requestCount);
        Assert.Equal(existing, result.FFMpegPath);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(existing));
    }

    [Fact]
    public async Task DownloadBinariesCore_UseSystemBinaries_ReturnsPathFromEnvironmentWithoutHttp()
    {
        Directory.CreateDirectory(_outputFolder);
        var systemBinDir = Path.Combine(_outputFolder, "system");
        Directory.CreateDirectory(systemBinDir);

        // Linux64 override → fileName resolves to "ffmpeg" (no .exe), which File.Exists matches on any OS.
        var systemFFMpeg = Path.Combine(systemBinDir, "ffmpeg");
        await File.WriteAllBytesAsync(systemFFMpeg, [0x99]);

        using var _ = EnvScope.Prepend("PATH", systemBinDir);

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new HttpClient(handler);

        var expectedOutputSubfolder = Path.Combine(_outputFolder, "out");

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: expectedOutputSubfolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.Prefer);

        Assert.Equal(0, requestCount);
        Assert.Equal(systemFFMpeg, result.FFMpegPath);
        Assert.False(Directory.Exists(expectedOutputSubfolder), "System-resolved binaries must not create an empty output folder");
    }

    [Fact]
    public async Task DownloadBinariesCore_UseSystemBinaries_FallsBackToDownloadWhenNotOnPath()
    {
        Directory.CreateDirectory(_outputFolder);
        var emptyPathDir = Path.Combine(_outputFolder, "empty-path");
        Directory.CreateDirectory(emptyPathDir);

        // Point PATH only at an empty directory so the binary cannot be found on the system.
        using var _ = EnvScope.Set("PATH", emptyPathDir);

        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
        using var client = CreateMockClient(json);

        var outputSubfolder = Path.Combine(_outputFolder, "out");

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: outputSubfolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.Prefer);

        Assert.NotNull(result.FFMpegPath);
        Assert.Equal(outputSubfolder, Path.GetDirectoryName(result.FFMpegPath));
        Assert.True(File.Exists(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_UseSystemBinaries_MixedResolution_OneFromPathOneDownloaded()
    {
        Directory.CreateDirectory(_outputFolder);
        var systemBinDir = Path.Combine(_outputFolder, "system");
        Directory.CreateDirectory(systemBinDir);

        // Only ffmpeg is present on the system; ffprobe must still be downloaded.
        var systemFFMpeg = Path.Combine(systemBinDir, "ffmpeg");
        await File.WriteAllBytesAsync(systemFFMpeg, [0xAB]);

        using var _ = EnvScope.Set("PATH", systemBinDir);

        var downloadedUrls = new List<string>();
        var gzContent = CreateGzipContent([0x03]);

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/releases/"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"),
                    ("ffprobe-linux-x64.gz", "https://mock/ffprobe-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            downloadedUrls.Add(url);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.Prefer);

        Assert.Equal(systemFFMpeg, result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
        Assert.Equal(_outputFolder, Path.GetDirectoryName(result.FFProbePath));
        Assert.True(File.Exists(result.FFProbePath));
        Assert.Single(downloadedUrls);
        Assert.Contains("ffprobe-linux-x64.gz", downloadedUrls[0]);
    }

    [Fact]
    public async Task DownloadBinaries_Public_NoNetworkWhenBinariesAlreadyExist()
    {
        // Exercises the public entry point (and its SharedHttpClient static-ctor path) without
        // any network I/O by pre-seeding the exact files the auto-detected platform expects.
        Directory.CreateDirectory(_outputFolder);
        var windows = OperatingSystem.IsWindows();
        var ffmpegFile = Path.Combine(_outputFolder, windows ? "ffmpeg.exe" : "ffmpeg");
        var ffprobeFile = Path.Combine(_outputFolder, windows ? "ffprobe.exe" : "ffprobe");
        await File.WriteAllBytesAsync(ffmpegFile, [0x01]);
        await File.WriteAllBytesAsync(ffprobeFile, [0x02]);

        var result = await FFMpegStaticFetcher.DownloadBinaries(outputFolder: _outputFolder);

        Assert.Equal(ffmpegFile, result.FFMpegPath);
        Assert.Equal(ffprobeFile, result.FFProbePath);
        Assert.Equal(new byte[] { 0x01 }, await File.ReadAllBytesAsync(ffmpegFile));
        Assert.Equal(new byte[] { 0x02 }, await File.ReadAllBytesAsync(ffprobeFile));
    }

    [Fact]
    public async Task DownloadBinariesCore_UseSystemBinaries_NullPath_FallsBackToDownload()
    {
        // Setting PATH to the empty string deletes the variable on Windows (so GetEnvironmentVariable
        // returns null) and leaves it empty on Unix — both hit the null/empty guard in GetFullPath.
        using var _ = EnvScope.Set("PATH", "");

        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
        using var client = CreateMockClient(json);

        var outputSubfolder = Path.Combine(_outputFolder, "out");

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: outputSubfolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.Prefer);

        Assert.NotNull(result.FFMpegPath);
        Assert.Equal(outputSubfolder, Path.GetDirectoryName(result.FFMpegPath));
        Assert.True(File.Exists(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_PreferIfCurrent_LocalNewer_UsesSystemNoDownload()
    {
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var binaryDownloads = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
            {
                var json = BuildReleaseJson("b6.1.1",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x01]))
            };
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.PreferIfCurrent,
            localVersionResolver: _ => new Version(7, 0));

        // Local 7.0 >= source 6.1.1 → keep the system binary, no binary download.
        Assert.Equal(systemFFMpeg, result.FFMpegPath);
        Assert.Equal(0, binaryDownloads);
    }

    [Fact]
    public async Task DownloadBinariesCore_PreferIfCurrent_LocalOlder_DownloadsManaged()
    {
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var binaryDownloads = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
            {
                var json = BuildReleaseJson("b6.1.1",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x01]))
            };
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.PreferIfCurrent,
            localVersionResolver: _ => new Version(6, 0));

        // Local 6.0 < source 6.1.1 → download the managed build into the output folder.
        Assert.Equal(1, binaryDownloads);
        Assert.Equal(_outputFolder, Path.GetDirectoryName(result.FFMpegPath));
        Assert.True(File.Exists(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_PreferIfCurrent_UnknownLocalVersion_KeepsSystem()
    {
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.PreferIfCurrent,
            localVersionResolver: _ => null);

        // Unknown local version under PreferIfCurrent → keep local, and don't even fetch the release.
        Assert.Equal(0, requestCount);
        Assert.Equal(systemFFMpeg, result.FFMpegPath);
    }

    [Fact]
    public async Task DownloadBinariesCore_PreferIfProvenCurrent_UnknownLocalVersion_Downloads()
    {
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var binaryDownloads = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
            {
                var json = BuildReleaseJson("b6.1.1",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x01]))
            };
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.PreferIfProvenCurrent,
            localVersionResolver: _ => null);

        // Unknown local version under PreferIfProvenCurrent → can't prove it's current → download.
        Assert.Equal(1, binaryDownloads);
        Assert.Equal(_outputFolder, Path.GetDirectoryName(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_Prefer_SkipsVersionCheckEntirely()
    {
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.Prefer,
            // Prefer must never run the version resolver; throwing here proves it isn't invoked.
            localVersionResolver: _ => throw new InvalidOperationException("version check must not run under Prefer"));

        Assert.Equal(0, requestCount);
        Assert.Equal(systemFFMpeg, result.FFMpegPath);
    }

    [Fact]
    public async Task DownloadBinariesCore_PreferIfCurrent_UrlOverrideSource_KeepsSystemWhenLocalKnown()
    {
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource
                {
                    FFMpegUrl = new Uri("https://mirror.example.com/ffmpeg-linux-x64.gz")
                }
            }
        };

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.PreferIfCurrent,
            settings: settings,
            localVersionResolver: _ => new Version(6, 0));

        // A direct-URL source has no comparable version → PreferIfCurrent keeps the system binary, no HTTP.
        Assert.Equal(0, requestCount);
        Assert.Equal(systemFFMpeg, result.FFMpegPath);
    }

    // Creates a fake `ffmpeg` (no extension, so File.Exists matches under the Linux64 override on any OS)
    // in a fresh directory and returns its full path. The caller prepends its directory to PATH.
    private async Task<string> CreateFakeSystemFFMpeg()
    {
        Directory.CreateDirectory(_outputFolder);
        var systemBinDir = Path.Combine(_outputFolder, "system");
        Directory.CreateDirectory(systemBinDir);

        var systemFFMpeg = Path.Combine(systemBinDir, "ffmpeg");
        await File.WriteAllBytesAsync(systemFFMpeg, [0x99]);

        return systemFFMpeg;
    }

    [Fact]
    public async Task DownloadBinariesCore_Reload_WipesFolderAndRedownloads()
    {
        Directory.CreateDirectory(_outputFolder);
        var stale = Path.Combine(_outputFolder, "ffmpeg");
        var unrelated = Path.Combine(_outputFolder, "leftover.txt");
        await File.WriteAllTextAsync(stale, "stale");
        await File.WriteAllTextAsync(unrelated, "leftover");

        var json = BuildReleaseJson("v1.0",
            ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));

        using var client = CreateMockClient(json);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            mode: FetchMode.Reload);

        Assert.NotNull(result.FFMpegPath);
        Assert.True(File.Exists(result.FFMpegPath));
        Assert.NotEqual("stale", await File.ReadAllTextAsync(result.FFMpegPath));
        Assert.False(File.Exists(unrelated), "Reload should wipe unrelated files in the output folder");
    }

    [Fact]
    public async Task DownloadBinariesCore_Download_WritesManifestWithReleaseTag()
    {
        var json = BuildReleaseJson("b6.1.1",
            ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));

        using var client = CreateMockClient(json);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        var manifest = await File.ReadAllTextAsync(Path.Combine(_outputFolder, ".ffmpeg-fetch.json"));
        Assert.Contains("b6.1.1", manifest);
    }

    [Fact]
    public async Task DownloadBinariesCore_ReFetchIfNewer_ReusesWhenTagMatches()
    {
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, [0x01, 0x02, 0x03]);
        // Manifest records the same tag the source reports → up to date, no binary re-download.
        await File.WriteAllTextAsync(Path.Combine(_outputFolder, ".ffmpeg-fetch.json"), "{\"tag\":\"v1.0\"}");

        var binaryDownloads = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/releases/"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0xFF]))
            };
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            mode: FetchMode.ReFetchIfNewer);

        Assert.Equal(0, binaryDownloads);
        Assert.Equal([0x01, 0x02, 0x03], await File.ReadAllBytesAsync(existing));
        Assert.Equal(existing, result.FFMpegPath);
    }

    [Fact]
    public async Task DownloadBinariesCore_ReFetchIfNewer_ReDownloadsWhenSourceTagDiffers()
    {
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, [0x01, 0x02, 0x03]);
        // Manifest records an older tag than the source's latest → stale, must re-download.
        await File.WriteAllTextAsync(Path.Combine(_outputFolder, ".ffmpeg-fetch.json"), "{\"tag\":\"v1.0\"}");

        var binaryDownloads = 0;
        var freshBytes = new byte[] { 0xAA, 0xBB };
        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/releases/"))
            {
                var json = BuildReleaseJson("v2.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent(freshBytes))
            };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            mode: FetchMode.ReFetchIfNewer);

        Assert.Equal(1, binaryDownloads);
        Assert.Equal(freshBytes, await File.ReadAllBytesAsync(existing));
        // Manifest is rewritten with the newer tag.
        Assert.Contains("v2.0", await File.ReadAllTextAsync(Path.Combine(_outputFolder, ".ffmpeg-fetch.json")));
    }

    [Fact]
    public async Task DownloadBinariesCore_ReFetchIfNewer_ReDownloadsWhenManifestMissing()
    {
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, [0x01]);
        // No .ffmpeg-fetch.json → unknown provenance → re-fetch to be safe.

        var binaryDownloads = 0;
        var freshBytes = new byte[] { 0x09 };
        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/releases/"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent(freshBytes))
            };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            mode: FetchMode.ReFetchIfNewer);

        Assert.Equal(1, binaryDownloads);
        Assert.Equal(freshBytes, await File.ReadAllBytesAsync(existing));
    }

    [Fact]
    public async Task DownloadBinariesCore_ReFetchIfNewer_DirectUrlOverride_ReusedWithoutGitHubCall()
    {
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, [0x01, 0x02, 0x03]);

        var requestCount = 0;
        var handler = new MockHttpMessageHandler(_ =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource
                {
                    FFMpegUrl = new Uri("https://mirror.example.com/ffmpeg-linux-x64.gz")
                }
            }
        };

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            mode: FetchMode.ReFetchIfNewer,
            settings: settings);

        // A direct-URL override has no release tag to compare, so the on-disk copy is reused with no HTTP at all.
        Assert.Equal(0, requestCount);
        Assert.Equal(existing, result.FFMpegPath);
        Assert.Equal([0x01, 0x02, 0x03], await File.ReadAllBytesAsync(existing));
    }

    [Fact]
    public async Task DownloadBinariesCore_UseExisting_DownloadsOnlyMissing()
    {
        Directory.CreateDirectory(_outputFolder);
        var existingFFMpeg = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existingFFMpeg, [0xAA]);

        var downloadedUrls = new List<string>();
        var gzContent = CreateGzipContent([0x02]);

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("/releases/"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"),
                    ("ffprobe-linux-x64.gz", "https://mock/ffprobe-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            downloadedUrls.Add(url);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.NotNull(result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
        Assert.Single(downloadedUrls);
        Assert.Contains("ffprobe-linux-x64.gz", downloadedUrls[0]);
        Assert.Equal([0xAA], await File.ReadAllBytesAsync(existingFFMpeg));
    }


    [Fact]
    public async Task DownloadBinariesCore_Settings_SourceOverrides_BypassesGitHub()
    {
        var gzContent = CreateGzipContent([0xCA, 0xFE]);
        var githubHits = 0;
        var directUrls = new List<string>();

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api.github.com"))
            {
                githubHits++;
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            directUrls.Add(url);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource
                {
                    FFMpegUrl = new Uri("https://mirror.example.com/ffmpeg-linux-x64.gz"),
                    FFProbeUrl = new Uri("https://mirror.example.com/ffprobe-linux-x64.gz")
                }
            }
        };

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            settings: settings);

        Assert.Equal(0, githubHits);
        Assert.Equal(2, directUrls.Count);
        Assert.Contains(directUrls, u => u.Contains("ffmpeg-linux-x64.gz"));
        Assert.Contains(directUrls, u => u.Contains("ffprobe-linux-x64.gz"));
        Assert.NotNull(result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
    }

    [Fact]
    public async Task DownloadBinariesCore_Settings_PartialOverride_FallsBackToGitHubForMissingBinary()
    {
        var gzContent = CreateGzipContent([0x42]);
        var githubReleaseFetched = false;
        var hitUrls = new List<string>();

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api.github.com"))
            {
                githubReleaseFetched = true;
                var json = BuildReleaseJson("v1.0",
                    ("ffprobe-linux-x64.gz", "https://github-cdn/ffprobe-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            hitUrls.Add(url);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource
                {
                    FFMpegUrl = new Uri("https://mirror.example.com/ffmpeg-linux-x64.gz")
                    // FFProbeUrl intentionally omitted
                }
            }
        };

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            settings: settings);

        Assert.True(githubReleaseFetched);
        Assert.Contains(hitUrls, u => u.Contains("mirror.example.com"));
        Assert.Contains(hitUrls, u => u.Contains("github-cdn"));
        Assert.NotNull(result.FFMpegPath);
        Assert.NotNull(result.FFProbePath);
    }

    [Fact]
    public async Task DownloadBinariesCore_Settings_RawBinary_WritesBytesVerbatim()
    {
        // Bytes that don't match any archive magic — should be treated as raw binary.
        var rawBytes = new byte[] { 0x7F, 0x45, 0x4C, 0x46, 0x02, 0x01, 0x01, 0x00 };

        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(rawBytes)
            });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource
                {
                    FFMpegUrl = new Uri("https://mirror.example.com/ffmpeg")
                }
            }
        };

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            settings: settings);

        Assert.NotNull(result.FFMpegPath);
        Assert.Equal(rawBytes, await File.ReadAllBytesAsync(result.FFMpegPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_Settings_CustomRepository_UsedInApiUrl()
    {
        string? capturedUrl = null;
        var gzContent = CreateGzipContent([0x01]);

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api.github.com"))
            {
                capturedUrl = url;
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings { Repository = "myorg/ffmpeg-fork" };

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            settings: settings);

        Assert.NotNull(capturedUrl);
        Assert.Contains("myorg/ffmpeg-fork", capturedUrl);
        Assert.DoesNotContain("eugeneware", capturedUrl);
    }


    [Fact]
    public async Task DownloadBinariesCore_Settings_HttpOverrideUrl_Rejected()
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x01]))
            });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource
                {
                    // Plaintext http must be refused — the downloaded executable would be MITM-replaceable.
                    FFMpegUrl = new Uri("http://mirror.example.com/ffmpeg-linux-x64.gz")
                }
            }
        };

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null,
                settings: settings));

        Assert.Contains("https", ex.Message);
    }

    [Fact]
    public async Task DownloadBinariesCore_EmptyDownloadBody_Throws()
    {
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            // 2xx with an empty body — must not be written through as a 0-byte binary.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            };
        });

        using var client = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_outputFolder, "ffmpeg")), "Empty download must not leave a binary");
        Assert.False(File.Exists(Path.Combine(_outputFolder, ".ffmpeg-fetch.json")), "A failed fetch must not advance the manifest tag");
    }

    [Fact]
    public async Task DownloadBinariesCore_ReuseExisting_PartialReuse_DoesNotAdvanceManifestTag()
    {
        Directory.CreateDirectory(_outputFolder);
        // ffmpeg present from an older release; ffprobe missing. The manifest records the old tag.
        await File.WriteAllBytesAsync(Path.Combine(_outputFolder, "ffmpeg"), [0xAA]);
        var manifestPath = Path.Combine(_outputFolder, ".ffmpeg-fetch.json");
        await File.WriteAllTextAsync(manifestPath, "{\"tag\":\"v1.0\"}");

        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
            {
                var json = BuildReleaseJson("v2.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"),
                    ("ffprobe-linux-x64.gz", "https://mock/ffprobe-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x02]))
            };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        // ffprobe was downloaded from v2.0, but ffmpeg on disk is still the old build — the manifest must NOT
        // claim v2.0, or a later ReFetchIfNewer would think the stale ffmpeg is current and skip its upgrade.
        Assert.True(File.Exists(Path.Combine(_outputFolder, "ffprobe")));
        Assert.DoesNotContain("v2.0", await File.ReadAllTextAsync(manifestPath));
    }

    [Fact]
    public async Task DownloadBinariesCore_SystemFFMpegPlusOverrideFFProbe_DoesNotWriteManifest()
    {
        // Regression: the release object is populated by the ffmpeg version-comparison probe, yet nothing is
        // downloaded from the catalog (ffmpeg is kept from the system, ffprobe comes from an https override).
        // The manifest must NOT record the probe's tag — no binary in the folder is a catalog build at that tag.
        var systemFFMpeg = await CreateFakeSystemFFMpeg();
        using var _ = EnvScope.Prepend("PATH", Path.GetDirectoryName(systemFFMpeg)!);

        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
            {
                var json = BuildReleaseJson("b6.1.1",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            // The https ffprobe override download.
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x01]))
            };
        });

        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource { FFProbeUrl = new Uri("https://mirror.example.com/ffprobe-linux-x64.gz") }
            }
        };

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            systemBinaries: SystemBinaryPolicy.PreferIfCurrent,
            settings: settings,
            localVersionResolver: _ => new Version(7, 0));

        // ffmpeg kept from system (7.0 >= 6.1.1), ffprobe from override → no catalog download → no manifest tag.
        Assert.Equal(systemFFMpeg, result.FFMpegPath);
        Assert.True(File.Exists(result.FFProbePath));
        Assert.False(File.Exists(Path.Combine(_outputFolder, ".ffmpeg-fetch.json")),
            "Manifest must not record a tag when no binary was downloaded from the release catalog");
    }

    [Fact]
    public async Task DownloadBinariesCore_RelativeOutputFolder_ReturnsAbsolutePath()
    {
        var relFolder = "rel-" + Guid.NewGuid().ToString("N");
        try
        {
            var json = BuildReleaseJson("v1.0", ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
            using var client = CreateMockClient(json);

            var result = await FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: relFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null);

            Assert.True(Path.IsPathRooted(result.FFMpegPath), $"Expected an absolute path, got '{result.FFMpegPath}'");
        }
        finally
        {
            var full = Path.GetFullPath(relFolder);
            if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadBinariesCore_HttpOverride_RejectedEvenWhenFileAlreadyExists()
    {
        Directory.CreateDirectory(_outputFolder);
        await File.WriteAllBytesAsync(Path.Combine(_outputFolder, "ffmpeg"), [0x01]);

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource { FFMpegUrl = new Uri("http://mirror.example.com/ffmpeg-linux-x64.gz") }
            }
        };

        // An insecure override must fail fast even when the binary is already on disk and would be reused.
        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null,
                settings: settings));

        Assert.Contains("https", ex.Message);
    }

    [Fact]
    public async Task DownloadBinariesCore_Reload_InsecureOverride_ThrowsBeforeWipingFolder()
    {
        // Regression: override-URL validation must run BEFORE the Reload folder wipe, so an insecure
        // configuration can never destroy the caller's existing binaries as a side effect of failing.
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, [0x01, 0x02, 0x03]);

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);

        var settings = new FetcherSettings
        {
            SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
            {
                [SupportedPlatform.Linux64] = new PlatformSource { FFMpegUrl = new Uri("http://mirror.example.com/ffmpeg-linux-x64.gz") }
            }
        };

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null,
                mode: FetchMode.Reload,
                settings: settings));

        Assert.Contains("https", ex.Message);
        // The pre-existing binary must survive untouched — the Reload wipe must not have run.
        Assert.True(File.Exists(existing), "Reload must not wipe the folder when the override config is rejected");
        Assert.Equal([0x01, 0x02, 0x03], await File.ReadAllBytesAsync(existing));
    }

    [Fact]
    public async Task DownloadBinariesCore_InvalidRepository_Throws()
    {
        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);

        var ex = await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null,
                settings: new FetcherSettings { Repository = "not a valid repo" }));

        Assert.Contains("repository", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadBinariesCore_GitHubToken_SentAsBearerOnApiRequestNotDownload()
    {
        string? releaseAuth = null;
        var downloadAuthSeen = false;
        string? downloadAuth = null;

        var handler = new MockHttpMessageHandler(request =>
        {
            var auth = request.Headers.Authorization?.ToString();
            if (request.RequestUri!.ToString().Contains("api.github.com"))
            {
                releaseAuth = auth;
                var json = BuildReleaseJson("v1.0", ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            downloadAuthSeen = true;
            downloadAuth = auth;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreateGzipContent([0x01])) };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null,
            settings: new FetcherSettings { GitHubToken = "tok123" });

        Assert.Equal("Bearer tok123", releaseAuth);
        Assert.True(downloadAuthSeen, "the asset download should have run");
        Assert.Null(downloadAuth); // token must never be sent to the asset/download host
    }

    [Fact]
    public async Task DownloadBinariesCore_ReuseExisting_ZeroByteOnDisk_ReDownloads()
    {
        Directory.CreateDirectory(_outputFolder);
        var existing = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(existing, Array.Empty<byte>()); // 0-byte leftover from a prior interrupted write

        var json = BuildReleaseJson("v1.0", ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
        var binaryDownloads = 0;
        var handler = new MockHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString().Contains("/releases/"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            binaryDownloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(CreateGzipContent([0x01, 0x02])) };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        // A 0-byte leftover is not trusted as reusable — it is re-downloaded and replaced.
        Assert.Equal(1, binaryDownloads);
        Assert.Equal([0x01, 0x02], await File.ReadAllBytesAsync(existing));
    }

    [Fact]
    public async Task DownloadBinariesCore_SystemBinaryInCurrentDirectory_ResolvedBeforePath()
    {
        // GetFullPath checks the current working directory before PATH; verify a CWD-local ffmpeg wins.
        Directory.CreateDirectory(_outputFolder);
        var cwdBinary = Path.Combine(_outputFolder, "ffmpeg");
        await File.WriteAllBytesAsync(cwdBinary, [0x99]);

        var handler = new MockHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(handler);

        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_outputFolder);

            var result = await FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: Path.Combine(_outputFolder, "out"),
                platformOverride: SupportedPlatform.Linux64,
                version: null,
                systemBinaries: SystemBinaryPolicy.Prefer);

            // Resolved from the CWD as an absolute path, with no download (a download would hit the 500 handler).
            // Assert by content + rootedness rather than string equality: SetCurrentDirectory may canonicalize
            // casing/short-names, so the returned path can differ textually while pointing at the same file.
            Assert.NotNull(result.FFMpegPath);
            Assert.True(Path.IsPathRooted(result.FFMpegPath));
            Assert.Equal([0x99], await File.ReadAllBytesAsync(result.FFMpegPath!));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
        }
    }

    [Fact]
    public async Task DownloadBinariesCore_EmptyFlag_DownloadsNothingAndReturnsNulls()
    {
        var handler = new MockHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("no HTTP calls expected")));

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: 0,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.Null(result.FFMpegPath);
        Assert.Null(result.FFProbePath);
    }

    [Fact]
    public async Task DownloadBinariesCore_UndefinedFlagBit_IgnoredAndReturnsNulls()
    {
        // GetFlags enumerates defined enum values, so extra bits are silently ignored.
        var handler = new MockHttpMessageHandler((Func<HttpRequestMessage, HttpResponseMessage>)(_ =>
            throw new InvalidOperationException("no HTTP calls expected")));

        using var client = new HttpClient(handler);

        var result = await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: (FFMpegBinary)0x80,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.Null(result.FFMpegPath);
        Assert.Null(result.FFProbePath);
    }

    [Fact]
    public async Task DownloadBinariesCore_CancellationToken_CancelsDownload()
    {
        var cts = new CancellationTokenSource();

        var handler = new MockHttpMessageHandler(async request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api.github.com"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            // Binary URL: cancel before returning, so the await on GetStreamAsync observes cancellation.
            cts.Cancel();
            await Task.Delay(50, CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(CreateGzipContent([0x01]))
            };
        });

        using var client = new HttpClient(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null,
                cancellationToken: cts.Token));
    }

    [Fact]
    public async Task DownloadBinariesCore_BothBinaries_DownloadsConcurrently()
    {
        var inFlight = 0;
        var peak = 0;
        var gate = new object();

        var handler = new MockHttpMessageHandler(async request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api.github.com"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"),
                    ("ffprobe-linux-x64.gz", "https://mock/ffprobe-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            lock (gate)
            {
                inFlight++;
                if (inFlight > peak) peak = inFlight;
            }

            try
            {
                await Task.Delay(100);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(CreateGzipContent([0x01, 0x02]))
                };
            }
            finally
            {
                lock (gate) inFlight--;
            }
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Linux64,
            version: null);

        Assert.Equal(2, peak);
    }

    [Fact]
    public async Task DownloadBinariesCore_AtomicWrite_NoStagingFileLeftOnFailure()
    {
        Directory.CreateDirectory(_outputFolder);
        var destination = Path.Combine(_outputFolder, "ffmpeg");
        var staging = destination + ".downloading";

        // Broken zip: zip magic but truncated body → ArchiveExtractor throws mid-extraction.
        var brokenBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00, 0x00 };
        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            if (url.Contains("api.github.com"))
            {
                var json = BuildReleaseJson("v1.0",
                    ("ffmpeg-linux-x64.gz", "https://mock/ffmpeg-linux-x64.gz"));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(brokenBytes)
            };
        });

        using var client = new HttpClient(handler);

        // Extraction failures are normalised to the library's own exception type.
        await Assert.ThrowsAsync<FFMpegStaticFetcherException>(() =>
            FFMpegStaticFetcher.DownloadBinariesCore(
                client,
                binaries: FFMpegBinary.FFMpeg,
                outputFolder: _outputFolder,
                platformOverride: SupportedPlatform.Linux64,
                version: null));

        Assert.False(File.Exists(destination), "Truncated binary must not be written to destination");
        Assert.False(File.Exists(staging), "Staging file must be cleaned up on extraction failure");
        Assert.False(File.Exists(Path.Combine(_outputFolder, ".ffmpeg-fetch.json")), "A failed fetch must not advance the manifest tag");
    }


    [Fact]
    public async Task DownloadBinariesCore_NullVersion_UsesLatestUrl()
    {
        string? capturedUrl = null;
        var gzContent = CreateGzipContent([0x01]);

        var handler = new MockHttpMessageHandler(request =>
        {
            var url = request.RequestUri!.ToString();

            if (url.Contains("/releases/"))
            {
                capturedUrl = url;
                var json = BuildReleaseJson("latest",
                    ("ffmpeg-win32-x64.gz", "https://mock/ffmpeg-win32-x64.gz"));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzContent)
            };
        });

        using var client = new HttpClient(handler);

        await FFMpegStaticFetcher.DownloadBinariesCore(
            client,
            binaries: FFMpegBinary.FFMpeg,
            outputFolder: _outputFolder,
            platformOverride: SupportedPlatform.Windows64,
            version: null);

        Assert.NotNull(capturedUrl);
        Assert.Contains("/releases/latest", capturedUrl);
    }
}

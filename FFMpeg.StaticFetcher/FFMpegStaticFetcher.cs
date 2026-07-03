using FFMpeg.StaticFetcher.Archives;
using FFMpeg.StaticFetcher.Enums;
using FFMpeg.StaticFetcher.Exceptions;
using FFMpeg.StaticFetcher.Extensions;
using FFMpeg.StaticFetcher.Models;
using FFMpeg.StaticFetcher.Platform;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FFMpeg.StaticFetcher;

public static class FFMpegStaticFetcher
{
    private const string DefaultRepository = "eugeneware/ffmpeg-static";
    private const string GitHubApiLatestFormat = "https://api.github.com/repos/{0}/releases/latest";
    private const string GitHubApiReleaseFormat = "https://api.github.com/repos/{0}/releases/tags/{1}";
    private const string DefaultOutputFolder = "./ffmpeg";
    private const string ManifestFileName = ".ffmpeg-fetch.json";
    private const long DefaultMaxDownloadBytes = 2L * 1024 * 1024 * 1024; // 2 GiB — generous for ffmpeg, stops decompression bombs.

    // Cached once rather than re-parsed on every call (these run at most twice per fetch).
    private static readonly Regex RepositoryPattern = new(@"^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$");
    private static readonly Regex VersionTokenPattern = new(@"version\s+(\d+(?:\.\d+){0,3})");
    private static readonly Regex TagVersionPattern = new(@"\d+(?:\.\d+){0,3}");

    private static readonly HttpClient SharedHttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        var name = typeof(FFMpegStaticFetcher).Namespace;
        // This library's own version, not the (possibly null) host entry assembly's.
        var version = typeof(FFMpegStaticFetcher).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        client.DefaultRequestHeaders.UserAgent.ParseAdd(string.Format("{0}/{1}", name, version));

        return client;
    }

    /// <summary>
    /// Download static FFMpeg/FFProbe binaries from eugeneware/ffmpeg-static GitHub releases
    /// (or any source you configure via <paramref name="settings"/>). Archive format is
    /// auto-detected from the downloaded content.
    /// </summary>
    /// <param name="binaries">Which binaries to download (FFMpeg, FFProbe, or both).</param>
    /// <param name="outputFolder">Directory where binaries will be saved. Defaults to "./ffmpeg" when null.</param>
    /// <param name="platformOverride">Force a specific platform instead of auto-detecting.</param>
    /// <param name="version">Specific release tag (e.g. "b6.1.1"). If null, the latest release is used.</param>
    /// <param name="mode">
    /// How to treat binaries already present in <paramref name="outputFolder"/>.
    /// <see cref="FetchMode.ReuseExisting"/> (default) keeps whatever is on disk and only downloads what is missing.
    /// <see cref="FetchMode.ReFetchIfNewer"/> re-downloads a binary when the source's resolved release tag differs from
    /// the one recorded in <c>.ffmpeg-fetch.json</c>. <see cref="FetchMode.Reload"/> deletes the output folder and
    /// re-downloads everything.
    /// </param>
    /// <param name="systemBinaries">
    /// Whether to use a machine-installed <c>ffmpeg</c>/<c>ffprobe</c> (current working directory or <c>PATH</c>)
    /// instead of downloading. <see cref="SystemBinaryPolicy.Ignore"/> (default) never looks at the machine.
    /// <see cref="SystemBinaryPolicy.Prefer"/> uses a system binary when present and skips the download.
    /// <see cref="SystemBinaryPolicy.PreferIfCurrent"/> / <see cref="SystemBinaryPolicy.PreferIfProvenCurrent"/>
    /// use the system binary only while it is at least as new as the source release (running <c>ffmpeg -version</c>
    /// to read its version), differing only in how they treat an undeterminable version. A system binary takes
    /// precedence over a copy already in <paramref name="outputFolder"/>.
    /// </param>
    /// <param name="settings">Optional source overrides — use a different GitHub repository or direct URLs per platform.</param>
    /// <param name="cancellationToken">Cancels the in-flight HTTP and file-write operations.</param>
    public static Task<FFMpegPaths> DownloadBinaries(FFMpegBinary binaries = FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
        string? outputFolder = null, SupportedPlatform? platformOverride = null, string? version = null,
        FetchMode mode = FetchMode.ReuseExisting, SystemBinaryPolicy systemBinaries = SystemBinaryPolicy.Ignore,
        FetcherSettings? settings = null, CancellationToken cancellationToken = default)
    {
        return DownloadBinariesCore(SharedHttpClient, binaries, outputFolder, platformOverride, version, mode, systemBinaries, settings, cancellationToken);
    }

    internal static async Task<FFMpegPaths> DownloadBinariesCore(HttpClient httpClient, FFMpegBinary binaries, string? outputFolder,
        SupportedPlatform? platformOverride, string? version, FetchMode mode = FetchMode.ReuseExisting,
        SystemBinaryPolicy systemBinaries = SystemBinaryPolicy.Ignore, FetcherSettings? settings = null,
        CancellationToken cancellationToken = default, Func<string, Version?>? localVersionResolver = null)
    {
        var asset = PlatformIdentifier.Resolve(platformOverride);
        // Absolutise once so the returned paths, manifest, and file operations all honor the documented
        // "absolute path" contract regardless of a relative outputFolder or the default "./ffmpeg".
        var binaryFolder = Path.GetFullPath(outputFolder ?? DefaultOutputFolder);

        PlatformSource? platformOverrideUrls = null;

        if (settings?.SourceOverrides is not null && settings.SourceOverrides.TryGetValue(asset.Platform, out var src)) platformOverrideUrls = src;

        // Validate override URLs BEFORE any destructive Reload wipe or download, so an insecure configuration
        // fails fast without side effects — a non-https override must not cost the caller their existing binaries.
        if (platformOverrideUrls is not null)
        {
            ValidateOverrideUrl(platformOverrideUrls.FFMpegUrl, "ffmpeg");
            ValidateOverrideUrl(platformOverrideUrls.FFProbeUrl, "ffprobe");
        }

        if (mode == FetchMode.Reload && Directory.Exists(binaryFolder)) Directory.Delete(binaryFolder, recursive: true);

        // Only ReFetchIfNewer consults the recorded tag; reading it lazily keeps the other modes IO-free.
        var recordedTag = mode == FetchMode.ReFetchIfNewer ? ReadManifestTag(binaryFolder) : null;

        // Tests inject a fake resolver so the version-comparison policies can run without a real ffmpeg on the box.
        localVersionResolver ??= ResolveLocalVersion;

        var gitHubToken = settings?.GitHubToken ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var maxDownloadBytes = settings?.MaxDownloadBytes ?? DefaultMaxDownloadBytes;

        string? ffmpegPath = null;
        string? ffprobePath = null;
        GitHubRelease? release = null;
        var downloads = new List<Task>();
        // The manifest tag may only be written when EVERY requested binary was freshly downloaded from the release
        // catalog: downloadedFromCatalog marks a real catalog fetch, mixedProvenance marks any reused/override/system
        // binary. A folder of mixed or reused provenance must not claim a single catalog tag — otherwise a later
        // ReFetchIfNewer would wrongly treat a stale or off-catalog binary as current and skip its upgrade. The
        // release object alone is an unreliable signal: it is also populated by version-comparison probes.
        var downloadedFromCatalog = false;
        var mixedProvenance = false;

        foreach (var binary in binaries.GetFlags())
        {
            var binaryName = binary switch
            {
                FFMpegBinary.FFMpeg => "ffmpeg",
                FFMpegBinary.FFProbe => "ffprobe",
                _ => throw new ArgumentOutOfRangeException(nameof(binary), binary, null)
            };

            var fileName = asset.IsWindows ? $"{binaryName}.exe" : binaryName;

            var overrideUrl = platformOverrideUrls is null ? null : binary switch
            {
                FFMpegBinary.FFMpeg => platformOverrideUrls.FFMpegUrl,
                FFMpegBinary.FFProbe => platformOverrideUrls.FFProbeUrl,
                _ => null
            };
            // The override scheme was validated up front (before the Reload wipe), so an insecure config
            // fails before any destructive or network side effect.

            if (systemBinaries != SystemBinaryPolicy.Ignore)
            {
                var systemPath = GetFullPath(fileName);

                if (systemPath is not null)
                {
                    var useSystem = systemBinaries == SystemBinaryPolicy.Prefer;

                    if (!useSystem)
                    {
                        // Version-checking policy: keep the system binary only while it is at least as new as the source.
                        var localVersion = localVersionResolver(systemPath);
                        Version? sourceVersion = null;

                        // Only the release catalog exposes a comparable version, and only worth fetching when the local one parsed.
                        if (localVersion is not null && overrideUrl is null)
                        {
                            release ??= await GetRelease(httpClient, settings?.Repository ?? DefaultRepository, version, gitHubToken, cancellationToken);
                            sourceVersion = ParseTagVersion(release.TagName);
                        }

                        // An undeterminable local OR source version falls back to the policy; otherwise keep local while current.
                        useSystem = (localVersion, sourceVersion) switch
                        {
                            (null, _) => systemBinaries == SystemBinaryPolicy.PreferIfCurrent,
                            (_, null) => systemBinaries == SystemBinaryPolicy.PreferIfCurrent,
                            var (local, source) => local >= source,
                        };
                    }

                    if (useSystem)
                    {
                        if (binary == FFMpegBinary.FFMpeg) ffmpegPath = systemPath;
                        else if (binary == FFMpegBinary.FFProbe) ffprobePath = systemPath;
                        mixedProvenance = true;
                        continue;
                    }
                }
            }

            var filePath = Path.Combine(binaryFolder, fileName);

            if (binary == FFMpegBinary.FFMpeg) ffmpegPath = filePath;
            else if (binary == FFMpegBinary.FFProbe) ffprobePath = filePath;

            // A single stat: FileInfo caches attributes on first access, so Exists and Length read one snapshot
            // (no TOCTOU gap, no double stat). A 0-byte leftover — e.g. a prior interrupted write — is not a
            // usable binary, so treat it as missing.
            var onDisk = new FileInfo(filePath);
            if (onDisk.Exists && onDisk.Length > 0)
            {
                // Anything reused from disk is not a fresh catalog download, so the folder's provenance is mixed.
                // ReuseExisting keeps whatever is on disk; Reload already wiped the folder above.
                if (mode != FetchMode.ReFetchIfNewer)
                {
                    mixedProvenance = true;
                    continue;
                }

                // A direct-URL override carries no release tag to compare against, so reuse it as-is.
                if (overrideUrl is not null)
                {
                    mixedProvenance = true;
                    continue;
                }

                // Reuse only while the on-disk tag matches the source's current release; otherwise re-download.
                release ??= await GetRelease(httpClient, settings?.Repository ?? DefaultRepository, version, gitHubToken, cancellationToken);
                if (string.Equals(release.TagName, recordedTag, StringComparison.Ordinal))
                {
                    mixedProvenance = true;
                    continue;
                }
            }

            // Deferred so callers that resolve everything from the system PATH don't get an empty output folder created.
            Directory.CreateDirectory(binaryFolder);

            Uri downloadUrl;

            if (overrideUrl is not null)
            {
                downloadUrl = overrideUrl; // scheme already validated up front
                mixedProvenance = true;    // override-sourced, carries no comparable release tag
            }
            else
            {
                // Lazy: only hit the GitHub catalog if at least one binary needs it.
                // Sequentially awaited here so we fetch the release exactly once even when both binaries need it.
                release ??= await GetRelease(httpClient, settings?.Repository ?? DefaultRepository, version, gitHubToken, cancellationToken);

                var assetName = $"{binaryName}-{asset.Os}-{asset.Arch}.gz";
                var releaseAsset = release.Assets?.FirstOrDefault(a => string.Equals(a.Name, assetName, StringComparison.Ordinal));

                if (releaseAsset?.BrowserDownloadUrl is null)
                    throw new FFMpegStaticFetcherException(
                        $"Asset '{assetName}' not found in release '{release.TagName}'",
                        $"Available assets: {string.Join(", ", release.Assets?.Select(a => a.Name) ?? [])}");

                downloadUrl = new Uri(releaseAsset.BrowserDownloadUrl);
                downloadedFromCatalog = true;
            }

            downloads.Add(DownloadExtractAndChmodAsync(httpClient, downloadUrl, fileName, filePath, maxDownloadBytes, cancellationToken));
        }

        if (downloads.Count > 0)
        {
            await Task.WhenAll(downloads);

            // Record the release tag so a later ReFetchIfNewer call can tell when a newer release ships — but only
            // when every requested binary was freshly downloaded from the catalog, so the tag never overstates a
            // reused, override-sourced, or system-resolved binary.
            if (release?.TagName is not null && downloadedFromCatalog && !mixedProvenance)
                WriteManifestTag(binaryFolder, release.TagName);
        }

        return new FFMpegPaths
        {
            FFMpegPath = ffmpegPath,
            FFProbePath = ffprobePath
        };
    }

    private static async Task DownloadExtractAndChmodAsync(HttpClient httpClient, Uri url, string expectedFile, string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        var tempArchive = Path.Combine(Path.GetTempPath(), $"ffmpeg-fetcher-{Guid.NewGuid():N}");
        try
        {
            await using (var responseStream = await httpClient.GetStreamAsync(url, cancellationToken))
            await using (var fileStream = File.Create(tempArchive))
            {
                await CopyBoundedAsync(responseStream, fileStream, maxBytes, url, cancellationToken);
            }

            // A 2xx response with an empty body would otherwise be copied through verbatim as a 0-byte binary
            // that File.Exists would later trust. Fail loudly instead.
            if (new FileInfo(tempArchive).Length == 0)
                throw new FFMpegStaticFetcherException($"Downloaded archive from {url} was empty (0 bytes)");

            ArchiveExtractor.ExtractBinary(tempArchive, expectedFile, destinationPath, maxBytes, cancellationToken);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                File.SetUnixFileMode(destinationPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FFMpegStaticFetcherException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Normalise transport/decompression failures (HttpRequestException, InvalidDataException,
            // SharpCompress errors) to the library's own type so callers can catch a single exception.
            throw new FFMpegStaticFetcherException($"Failed to download or extract '{expectedFile}' from {url}", ex.Message, ex);
        }
        finally
        {
            // Best-effort: a locked temp file must never mask the real failure above.
            try { if (File.Exists(tempArchive)) File.Delete(tempArchive); } catch { /* ignored */ }
        }
    }

    // Caps the bytes pulled from the network so a server can't stream an unbounded body to fill the temp volume.
    private static async Task CopyBoundedAsync(Stream source, Stream destination, long maxBytes, Uri url, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;

            if (total > maxBytes)
                throw new FFMpegStaticFetcherException($"Download from {url} exceeded the maximum of {maxBytes} bytes");

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static async Task<GitHubRelease> GetRelease(HttpClient client, string repository, string? version, string? token, CancellationToken cancellationToken)
    {
        // Validate the owner/repo shape so it can't inject extra path segments, and percent-encode the tag so a
        // value with '#', '?', or '..' can't rewrite the request path (Uri collapses dot-segments before sending).
        if (!RepositoryPattern.IsMatch(repository))
            throw new FFMpegStaticFetcherException($"Invalid repository '{repository}'", "Expected GitHub 'owner/repo' form.");

        var url = version is null
            ? string.Format(GitHubApiLatestFormat, repository)
            : string.Format(GitHubApiReleaseFormat, repository, Uri.EscapeDataString(version));

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        // Sent only to api.github.com (this method), never to asset/override download hosts.
        if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new FFMpegStaticFetcherException($"Failed to get release info from {url} (HTTP {(int)response.StatusCode})", body);

        var release = JsonSerializer.Deserialize<GitHubRelease>(body);

        return release ?? throw new FFMpegStaticFetcherException($"Failed to deserialize release info from {url}", body);
    }

    internal static Version? ResolveLocalVersion(string exePath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);

            if (process is null) return null;

            // Drain BOTH pipes asynchronously: if either the stdout or the (redirected) stderr buffer fills, the
            // child blocks on the write and never exits, so an undrained stream would force the timeout below.
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }

                // The reads are abandoned; observe them so a fault from the torn-down pipe can't go unobserved.
                ObserveFault(outputTask);
                ObserveFault(errorTask);

                return null;
            }

            var output = outputTask.GetAwaiter().GetResult();
            ObserveFault(errorTask);

            return ParseVersionToken(output);
        }
        catch
        {
            // Any failure to run or read the binary is treated as "unknown version".
            return null;
        }
    }

    private static void ObserveFault(Task task) =>
        task.ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    // "ffmpeg version 6.1.1-full_build..." → 6.1.1; a git build ("ffmpeg version N-113802-g...") has no semver → null.
    internal static Version? ParseVersionToken(string text)
    {
        var match = VersionTokenPattern.Match(text);

        return match.Success ? NormalizeVersion(match.Groups[1].Value) : null;
    }

    // Release tags carry a vendor prefix ("b6.1.1", "v1.0") — take the first numeric run.
    internal static Version? ParseTagVersion(string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;

        var match = TagVersionPattern.Match(tag);

        return match.Success ? NormalizeVersion(match.Value) : null;
    }

    private static Version? NormalizeVersion(string raw)
    {
        // System.Version requires at least major.minor; pad a bare "6" to "6.0".
        var normalized = raw.Contains('.') ? raw : raw + ".0";

        return Version.TryParse(normalized, out var version) ? version : null;
    }

    // Enforce the https-only override contract before any side effect. Called up front so a misconfigured
    // (non-https or relative) override URL fails before the Reload wipe, a network request, or a file write.
    private static void ValidateOverrideUrl(Uri? url, string binaryName)
    {
        if (url is not null && (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps))
            throw new FFMpegStaticFetcherException(
                $"Source override URL for '{binaryName}' must be an absolute https URL (got '{url}')",
                "Set FetcherSettings.SourceOverrides URLs to https:// endpoints.");
    }

    private static string? ReadManifestTag(string binaryFolder)
    {
        var path = Path.Combine(binaryFolder, ManifestFileName);

        if (!File.Exists(path)) return null;

        try
        {
            return JsonSerializer.Deserialize<FetchManifest>(File.ReadAllText(path))?.Tag;
        }
        catch
        {
            // A missing or unreadable manifest is treated as "unknown tag", which triggers a re-fetch.
            return null;
        }
    }

    private static void WriteManifestTag(string binaryFolder, string tag)
    {
        var path = Path.Combine(binaryFolder, ManifestFileName);

        File.WriteAllText(path, JsonSerializer.Serialize(new FetchManifest { Tag = tag }));
    }

    private static string? GetFullPath(string fileName)
    {
        if (File.Exists(fileName)) return Path.GetFullPath(fileName);

        var values = Environment.GetEnvironmentVariable("PATH");

        if (values == null || values.Length == 0) return default;

        foreach (var path in values.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return default;
    }
}

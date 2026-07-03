# FFMpeg.StaticFetcher

<p align="center">
  <a href="https://www.nuget.org/packages/FFMpeg.StaticFetcher"><img src="https://img.shields.io/nuget/v/FFMpeg.StaticFetcher.svg?label=NuGet&color=blue" alt="NuGet version" /></a>
  <a href="https://www.nuget.org/packages/FFMpeg.StaticFetcher"><img src="https://img.shields.io/nuget/dt/FFMpeg.StaticFetcher.svg?label=Downloads&color=blue" alt="NuGet downloads" /></a>
  <a href="https://github.com/Alpaq92/FFMpeg.StaticFetcher/actions/workflows/ci.yml"><img src="https://img.shields.io/github/actions/workflow/status/Alpaq92/FFMpeg.StaticFetcher/ci.yml?branch=main&label=CI" alt="CI" /></a>
  <a href="https://github.com/Alpaq92/FFMpeg.StaticFetcher/actions/workflows/release.yml"><img src="https://img.shields.io/github/actions/workflow/status/Alpaq92/FFMpeg.StaticFetcher/release.yml?branch=main&label=Release" alt="Release" /></a>
  <a href="LICENSE.md"><img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License: MIT" /></a>
</p>

A small .NET 8 library that downloads static **FFmpeg** and **FFprobe** binaries. It auto-detects your OS and architecture, auto-detects the downloaded archive format (gzip, xz, bzip2, zip, 7z, rar, their `tar.*` variants, plain tar, or a raw binary), and leaves ready-to-run executables in your output folder.

## Installation

```bash
dotnet add package FFMpeg.StaticFetcher
```

## Quick start

```csharp
using System.Diagnostics;
using FFMpeg.StaticFetcher;

// Download the latest ffmpeg + ffprobe to ./ffmpeg
var binaries = await FFMpegStaticFetcher.DownloadBinaries();

using var process = Process.Start(new ProcessStartInfo
{
    FileName = binaries.FFMpegPath,
    Arguments = "-i input.mp4 -vn -acodec libmp3lame output.mp3",
    UseShellExecute = false,
    CreateNoWindow = true
});
await process!.WaitForExitAsync();
```

## API

```csharp
public static Task<FFMpegPaths> DownloadBinaries(
    FFMpegBinary binaries = FFMpegBinary.FFMpeg | FFMpegBinary.FFProbe,
    string? outputFolder = null,
    SupportedPlatform? platformOverride = null,
    string? version = null,
    FetchMode mode = FetchMode.ReuseExisting,
    SystemBinaryPolicy systemBinaries = SystemBinaryPolicy.Ignore,
    FetcherSettings? settings = null,
    CancellationToken cancellationToken = default)
```

| Parameter | Type | Default | Description |
|---|---|---|---|
| `binaries` | `FFMpegBinary` | `FFMpeg \| FFProbe` | Which binaries to download. `[Flags]` enum — combine with `\|`. |
| `outputFolder` | `string?` | `null` | Where binaries are saved. Defaults to `./ffmpeg`. |
| `platformOverride` | `SupportedPlatform?` | `null` | Force a platform instead of auto-detecting. See [Supported platforms](#supported-platforms). |
| `version` | `string?` | `null` | A GitHub release tag (e.g. `"b6.1.1"`); `null` downloads the latest. |
| `mode` | `FetchMode` | `ReuseExisting` | How to treat binaries already on disk. See [Fetch modes](#fetch-modes). |
| `systemBinaries` | `SystemBinaryPolicy` | `Ignore` | Whether to use a machine-installed `ffmpeg`/`ffprobe` instead of downloading. See [System-installed FFmpeg](#prefer-a-system-installed-ffmpeg). |
| `settings` | `FetcherSettings?` | `null` | Change the source repository or point at direct URLs. See [Custom sources](#custom-sources). |
| `cancellationToken` | `CancellationToken` | `default` | Cancels the in-flight HTTP and file-write operations. |

**Returns** an `FFMpegPaths` record: `FFMpegPath` and `FFProbePath` (`string?`), each an absolute path — or `null` if that binary wasn't requested.

## Examples

```csharp
// Only ffmpeg (FFProbePath will be null)
await FFMpegStaticFetcher.DownloadBinaries(binaries: FFMpegBinary.FFMpeg);

// Custom output folder
await FFMpegStaticFetcher.DownloadBinaries(outputFolder: "./bin");

// Pin a release tag
await FFMpegStaticFetcher.DownloadBinaries(version: "b6.1.1");

// Force a platform
await FFMpegStaticFetcher.DownloadBinaries(platformOverride: SupportedPlatform.LinuxArm64);
```

### Fetch modes

`FetchMode` controls what happens when binaries already exist in the output folder:

```csharp
// ReuseExisting (default): reuse matching binaries on disk, download only what's
// missing — no network call when nothing is missing.
await FFMpegStaticFetcher.DownloadBinaries();

// ReFetchIfNewer: reuse on disk only while current; re-download when a newer build ships.
await FFMpegStaticFetcher.DownloadBinaries(mode: FetchMode.ReFetchIfNewer);

// Reload: delete the output folder and download everything again.
await FFMpegStaticFetcher.DownloadBinaries(mode: FetchMode.Reload);
```

`ReFetchIfNewer` records the resolved release tag in `.ffmpeg-fetch.json`; on later calls it re-downloads only when the source's current tag differs from the recorded one. Binaries resolved from a direct-URL override carry no tag and are always reused in this mode.

### Prefer a system-installed FFmpeg

```csharp
// Use ffmpeg/ffprobe already on the machine (working directory or PATH) instead of
// downloading — no network call, and ./ffmpeg is not created. If only one is found,
// the other is downloaded. A system binary takes precedence over a copy in the output folder.
await FFMpegStaticFetcher.DownloadBinaries(systemBinaries: SystemBinaryPolicy.Prefer);
```

`PreferIfCurrent` / `PreferIfProvenCurrent` keep the system binary only while it's at least as new as the source release (read by running the binary with `-version`). They differ only when a comparable version can't be established — either the local build has no semantic version (git/nightly), or a direct-URL source carries no release tag:

| Policy | Local version unknown |
|---|---|
| `PreferIfCurrent` | **Keep** the local binary (no surprise downloads) |
| `PreferIfProvenCurrent` | **Download** the managed build (don't trust an unverifiable binary) |

> **Note:** these two policies execute the discovered binary (`-version`, 5-second timeout) to read its version — the specific binary being checked (`ffmpeg -version`, `ffprobe -version`), found in the **current working directory first, then `PATH`** (see [Security](#security)). `Prefer` never runs the binary.

## Custom sources

By default, binaries come from the [`eugeneware/ffmpeg-static`](https://github.com/eugeneware/ffmpeg-static) GitHub releases. `FetcherSettings` lets you point at a different repository or bypass GitHub entirely — useful if upstream disappears, gets rate-limited, or you need a private mirror.

```csharp
public record FetcherSettings
{
    public string? Repository { get; init; }                                             // "owner/repo"
    public IReadOnlyDictionary<SupportedPlatform, PlatformSource>? SourceOverrides { get; init; }
    public string? GitHubToken { get; init; }      // raises the GitHub API rate limit
    public long? MaxDownloadBytes { get; init; }   // decompression-bomb ceiling, default 2 GiB
}

public record PlatformSource
{
    public Uri? FFMpegUrl { get; init; }
    public Uri? FFProbeUrl { get; init; }
}
```

- **`GitHubToken`** is sent as a `Bearer` token on `api.github.com` release-metadata requests **only** (never on downloads), raising GitHub's anonymous 60 req/hr limit. Falls back to the `GITHUB_TOKEN` environment variable when `null` — handy in CI.
- **`MaxDownloadBytes`** caps bytes read and written per binary (default **2 GiB**); exceeding it throws.
- **`SourceOverrides` URLs must be `https`** (validated up front) and **`Repository` must be `owner/repo`** (validated when the release catalog is actually queried — a full override never reaches it). A violation throws an `FFMpegStaticFetcherException`.

**Resolution order, per requested binary:** a matching `SourceOverrides` URL if present (no GitHub call), otherwise the matching asset from `Repository`'s release catalog. The archive format is auto-detected from magic bytes, so any supported format works; for multi-file archives the entry named `ffmpeg`/`ffprobe` (with `.exe` on Windows) is extracted and the archive discarded.

```csharp
var settings = new FetcherSettings
{
    // A different fork with the same {binary}-{os}-{arch}.gz asset naming:
    Repository = "myorg/ffmpeg-static-mirror",

    // ...or direct per-platform URLs (a .gz, or a zip containing the executables):
    SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
    {
        [SupportedPlatform.Linux64]   = new() { FFMpegUrl = new("https://cdn.example.com/ffmpeg-linux-x64.gz"),
                                                FFProbeUrl = new("https://cdn.example.com/ffprobe-linux-x64.gz") },
        [SupportedPlatform.Windows64] = new() { FFMpegUrl = new("https://cdn.example.com/ffmpeg-win64.zip"),
                                                FFProbeUrl = new("https://cdn.example.com/ffmpeg-win64.zip") }
    }
};

await FFMpegStaticFetcher.DownloadBinaries(settings: settings);
```

Overrides are partial-friendly: set `FFMpegUrl` but leave `FFProbeUrl` `null`, and `ffprobe` still comes from the release catalog.

## Supported platforms

| Enum value | OS | Architecture | Asset identifier |
|---|---|---|---|
| `Windows64` | Windows | x86_64 | `win32-x64` |
| `Linux64` | Linux | x86_64 | `linux-x64` |
| `Linux32` | Linux | x86 | `linux-ia32` |
| `LinuxArm` | Linux | ARMv7 (armhf) | `linux-arm` |
| `LinuxArm64` | Linux | ARM64 (aarch64) | `linux-arm64` |
| `Osx64` | macOS | x86_64 (Intel) | `darwin-x64` |
| `OsxArm64` | macOS | ARM64 (Apple Silicon) | `darwin-arm64` |

Without a `platformOverride`, the platform is auto-detected via `RuntimeInformation`.

> **Windows on ARM64:** upstream publishes no native `win32-arm64` build, so auto-detection falls back to `Windows64` (x86_64), which runs under the Windows-on-ARM x64 emulation layer.

## Available binaries

`FFMpegBinary` is a `[Flags]` enum: `FFMpeg` (`ffmpeg`/`ffmpeg.exe`) and `FFProbe` (`ffprobe`/`ffprobe.exe`). The default downloads both.

## How it works

1. Resolves the current platform to an `{os}-{arch}` identifier (and validates any `SourceOverrides` URLs up front).
2. If `mode: Reload`, wipes the output folder.
3. If `systemBinaries` isn't `Ignore`, searches the working directory and `PATH` — `Prefer` returns that path; the version-aware policies additionally run the binary with `-version` and keep it only while current.
4. For each remaining binary, reuses the file already in the output folder — unless it's missing, or `ReFetchIfNewer` finds the source's release tag differs from `.ffmpeg-fetch.json`.
5. Resolves the download URL (a `SourceOverrides` entry, else the matching GitHub release asset), streams it to a temp file (rejecting an empty body), auto-detects the format by magic bytes, and extracts through a `.downloading` staging file that is atomically moved into place.
6. Adds `.exe` on Windows / `chmod 755` on Unix, and records the release tag for future `ReFetchIfNewer` calls.

Network and extraction failures (HTTP errors, empty/corrupt downloads, a missing archive entry, a rejected non-`https` override) all surface as `FFMpegStaticFetcherException`, whose `Detail` property carries diagnostic context.

## Using with wrapper libraries

The output paths plug straight into FFmpeg wrappers. With [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore), point its global options at the folder this library wrote to:

```csharp
var binaries = await FFMpegStaticFetcher.DownloadBinaries();

GlobalFFOptions.Configure(new FFOptions
{
    BinaryFolder = Path.GetDirectoryName(binaries.FFMpegPath!)!
});
```

Any library that accepts an `ffmpeg` path or folder can be fed `binaries.FFMpegPath` / `Path.GetDirectoryName(binaries.FFMpegPath)`.

## Security

This library downloads native executables and hands you their paths to run. For your threat model:

- **Binaries are not checksum- or signature-verified.** Trust rests on TLS plus the integrity of the source. This matches comparable binary-fetcher tools; if you need more, verify the returned binary against a known hash before executing it.
- **Override URLs must use `https`** — a non-`https` `SourceOverrides` URL is rejected, so a plaintext mirror can't be swapped in on a hostile network. The default GitHub endpoints are hardcoded `https`.
- **`PreferIfCurrent` / `PreferIfProvenCurrent` execute a discovered binary**, checking the working directory *before* `PATH`. Don't enable them where the working directory is writable by a less-trusted party, or a planted `./ffmpeg` would run first. The default (`Ignore`) never touches the machine.
- **Archive extraction targets a fixed path.** Entries are matched by filename only and written to the output folder (no zip-slip); symlink/hardlink entries are skipped in favor of the real file.
- **Downloads and decompression are size-bounded** by `MaxDownloadBytes` (default 2 GiB), guarding against a decompression bomb.

## FFmpeg licensing note

The downloaded binaries are [GPL v2+](https://www.ffmpeg.org/legal.html) (the `eugeneware/ffmpeg-static` builds include components like x264/x265).

- **Running FFmpeg** as a separate process does **not** make your app a derivative work — GPL imposes no obligations on software that merely executes a GPL binary. You can use the binaries in commercial/proprietary apps.
- **GPL obligations trigger only on distribution.** If you *redistribute* the binaries (e.g. bundle them in an installer), you must comply with the GPL.
- **This library fetches binaries at runtime**, so your app doesn't bundle or redistribute FFmpeg — the end user's machine downloads them directly.

See the [FFmpeg Legal page](https://www.ffmpeg.org/legal.html) and [GPL FAQ](https://www.gnu.org/licenses/gpl-faq.html) for details.

## Binary source

Binaries come from [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static), which repackages official builds from [gyan.dev](https://www.gyan.dev/ffmpeg/builds/) (Windows), [johnvansickle.com](https://johnvansickle.com/ffmpeg/) (Linux), [evermeet.cx](https://evermeet.cx/pub/ffmpeg/) (macOS Intel), and [osxexperts.net](https://osxexperts.net/) (Apple Silicon).

## Testing

```bash
dotnet test --filter "Category!=Integration"   # fast, offline suite (the CI gate)
dotnet test                                    # includes live-network integration tests
```

Live-network tests are tagged `[Trait("Category", "Integration")]` — they download real binaries and can take minutes. CI runs the offline suite as the gate and the integration suite as a separate non-blocking lane. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full test and CI layout.

Offline coverage (Windows run):

| Line | Branch | Method |
|---|---|---|
| 96.1% | 91.0% | 100% (60/60) |

The uncovered lines are the `XZ` decompression path (SharpCompress ships no xz writer to build a fixture) and the Unix-only `chmod 755` branch — the latter runs on the Linux CI lane.

## Requirements

- .NET 8.0 or later

## Dependencies

The **entire** dependency graph is a single MIT-licensed package: **[SharpCompress](https://github.com/adamhathcock/sharpcompress)** ([MIT](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt)), used to extract zip/7z/rar/tar/`tar.*`/bzip2/xz archives. Gzip (the default `eugeneware/ffmpeg-static` path) uses the BCL's `GZipStream`, not SharpCompress. SourceLink is provided by the .NET 8+ SDK — no package reference needed.

Dependency updates and releases are automated via Dependabot + GitHub Actions — see [CONTRIBUTING.md](CONTRIBUTING.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for notable changes, including breaking changes and migration notes.

## License

- **Source code** — [MIT](LICENSE.md).
- **Project icon** (`icon.png`) — a third-party asset **not** covered by MIT; distributed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) (see [Credits](#credits)).
- **Downloaded FFmpeg/FFProbe binaries** — not distributed with this project; they retain their own licenses (see the [FFmpeg licensing note](#ffmpeg-licensing-note)).

## Credits

- **Binaries** — [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static).
- **Inspiration** — [FFMpegCore.Extensions.Downloader](https://github.com/rosenbjerg/FFMpegCore).
- **Icon** — the [Jellyfin FFmpeg icon](https://commons.wikimedia.org/wiki/File:Jellyfin_-_icon-transparent-ffmpeg.svg) by the [Jellyfin project](https://github.com/jellyfin/jellyfin-ux), [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/), used unmodified.

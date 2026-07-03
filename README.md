# FFMpeg.StaticFetcher

[![NuGet](https://img.shields.io/nuget/v/FFMpeg.StaticFetcher.svg)](https://www.nuget.org/packages/FFMpeg.StaticFetcher)
[![CI](https://img.shields.io/github/actions/workflow/status/Alpaq92/FFMpeg.StaticFetcher/ci.yml?branch=main&label=CI)](https://github.com/Alpaq92/FFMpeg.StaticFetcher/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.md)

A small .NET 8 library that downloads static FFMpeg and FFProbe binaries.

Automatically detects the current OS and architecture, auto-detects the downloaded archive format (gzip, xz, bzip2, zip, 7z, rar, tar.gz, tar.xz, tar.bz2, plain tar, or raw binary), and leaves a ready-to-run executable in your output folder.

## Installation

```bash
dotnet add package FFMpeg.StaticFetcher
```

## Quick start

```csharp
using System.Diagnostics;
using FFMpeg.StaticFetcher;

// Download latest ffmpeg + ffprobe to the default output folder
var binaries = await FFMpegStaticFetcher.DownloadBinaries();

var arguments = "-i input.mp4 -vn -acodec libmp3lame output.mp3";

var psi = new ProcessStartInfo
{
    FileName = binaries.FFMpegPath,
    Arguments = arguments,
    RedirectStandardInput = true,
    RedirectStandardError = true,
    RedirectStandardOutput = true,
    UseShellExecute = false,
    CreateNoWindow = true
};

using var process = Process.Start(psi);
await process!.WaitForExitAsync();
```

## API

### `FFMpegStaticFetcher.DownloadBinaries`

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
| `binaries` | `FFMpegBinary` | `FFMpeg \| FFProbe` | Which binaries to download. Flags enum — combine with `\|`. |
| `outputFolder` | `string?` | `null` | Directory where binaries will be saved. Defaults to `./ffmpeg` when `null`. |
| `platformOverride` | `SupportedPlatform?` | `null` | Force a specific platform instead of auto-detecting from the runtime. |
| `version` | `string?` | `null` | A specific GitHub release tag (e.g. `"b6.1.1"`). Downloads the latest release when `null`. |
| `mode` | `FetchMode` | `ReuseExisting` | How to treat binaries already in the output folder. `ReuseExisting` (default) reuses any matching binaries on disk and only downloads the missing ones (no HTTP call when nothing is missing). `ReFetchIfNewer` re-downloads a binary when the source's resolved release tag differs from the one recorded in `.ffmpeg-fetch.json`. `Reload` recursively deletes the output folder and redownloads everything. |
| `systemBinaries` | `SystemBinaryPolicy` | `Ignore` | Whether to use a machine-installed `ffmpeg`/`ffprobe` (current working directory or `PATH`) instead of downloading. `Ignore` (default) never looks at the machine. `Prefer` uses a system binary when present and skips the download. `PreferIfCurrent` / `PreferIfProvenCurrent` use the system binary only while it is at least as new as the source release — they run `ffmpeg -version` to read its version and differ only in how they treat an undeterminable version (`PreferIfCurrent` keeps it, `PreferIfProvenCurrent` downloads). A system binary takes precedence over a copy already in the output folder; no empty output folder is created when everything resolves from the system. See [System-installed FFmpeg](#prefer-a-system-installed-ffmpeg). |
| `settings` | `FetcherSettings?` | `null` | Override the source repository and/or provide direct per-platform URLs. See [Custom sources](#custom-sources). |
| `cancellationToken` | `CancellationToken` | `default` | Cancels the in-flight HTTP and file-write operations. |

**Returns** an `FFMpegPaths` record with named properties for each requested binary. Properties for binaries that were not requested are `null`.

| Property | Type | Description |
|---|---|---|
| `FFMpegPath` | `string?` | Absolute path to the `ffmpeg` binary, or `null` if not requested. |
| `FFProbePath` | `string?` | Absolute path to the `ffprobe` binary, or `null` if not requested. |

## Examples

### Download only ffmpeg

```csharp
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    binaries: FFMpegBinary.FFMpeg);

// binaries.FFMpegPath is set; binaries.FFProbePath is null
```

### Download to a custom folder

```csharp
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    outputFolder: "./bin");
```

### Pin a specific version

```csharp
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    version: "b6.1.1");
```

### Force a platform

```csharp
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    platformOverride: SupportedPlatform.LinuxArm64);
```

### Reuse binaries already on disk

```csharp
// If ./ffmpeg/ffmpeg(.exe) and ./ffmpeg/ffprobe(.exe) already exist,
// this call returns their paths without hitting the network. This is the
// default (FetchMode.ReuseExisting).
var binaries = await FFMpegStaticFetcher.DownloadBinaries();
```

### Reuse what's on disk, but refresh when a newer build ships

```csharp
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    mode: FetchMode.ReFetchIfNewer);
```

On the first download the resolved release tag is recorded in `.ffmpeg-fetch.json` inside the output folder. On later `ReFetchIfNewer` calls the library asks the source for its current release tag and re-downloads only when it differs from the recorded one — a matching tag still skips the binary download. Because the GitHub `latest` endpoint always returns the newest release, a differing tag means a newer build is available. Binaries resolved from a direct-URL override carry no tag and are always reused in this mode.

### Clean re-download (force refresh)

```csharp
// Deletes the output folder and downloads everything again.
var binaries = await FFMpegStaticFetcher.DownloadBinaries(mode: FetchMode.Reload);
```

### Prefer a system-installed FFmpeg

```csharp
// If ffmpeg/ffprobe are already installed on the machine (on PATH or in the
// current working directory), this returns those absolute paths without hitting
// the network and without creating ./ffmpeg. If only one is found on the system,
// the other is downloaded as usual.
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    systemBinaries: SystemBinaryPolicy.Prefer);
```

### Use the system FFmpeg only when it isn't out of date

```csharp
// Use the installed ffmpeg/ffprobe only while it's at least as new as the source
// release; otherwise download the managed build. The local version is read by
// running `ffmpeg -version`.
var binaries = await FFMpegStaticFetcher.DownloadBinaries(
    systemBinaries: SystemBinaryPolicy.PreferIfCurrent);
```

`PreferIfCurrent` and `PreferIfProvenCurrent` compare the **local** ffmpeg's version against the source's current release tag and keep the local binary while it's current. They differ only when the local version can't be determined — a git/nightly build whose `ffmpeg -version` has no semantic version, or a direct-URL source that carries no release tag:

| Policy | Local version unknown |
|---|---|
| `PreferIfCurrent` | **Keep** the local binary (no surprise downloads) |
| `PreferIfProvenCurrent` | **Download** the managed build (don't trust an unverifiable local binary) |

> **Note:** these two policies execute the discovered `ffmpeg` (with `-version`, guarded by a 5-second timeout) to read its version. Version comparison only works against the GitHub release catalog; a direct-URL override has no version to compare, so it falls back to the policy's unknown-version behavior above. `Prefer` never runs the binary.

## Custom sources

By default, binaries are pulled from the [`eugeneware/ffmpeg-static`](https://github.com/eugeneware/ffmpeg-static) GitHub releases. `FetcherSettings` lets you **point at a different repository or bypass GitHub entirely** — useful if the upstream repo disappears, becomes rate-limited, or you need to pin to a private mirror on your own CDN.

```csharp
public record FetcherSettings
{
    public string? Repository { get; init; }
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

- **`GitHubToken`** is sent as a `Bearer` token on `api.github.com` release-metadata requests **only** (never on asset or override downloads), raising GitHub's anonymous 60 requests/hour limit. When `null`, the `GITHUB_TOKEN` environment variable is used if present — handy in CI.
- **`MaxDownloadBytes`** caps the bytes read from a download and written while decompressing a single binary (default **2 GiB**); exceeding it throws, guarding against a hostile mirror serving a decompression bomb.
- **`Repository`** must be in `owner/repo` form, and `SourceOverrides` URLs must use `https` — both are validated and rejected with an `FFMpegStaticFetcherException` otherwise.

Resolution order **per requested binary**:

1. If `SourceOverrides` has an entry for the resolved platform **and** that entry has a URL for the requested binary, use it (no GitHub call).
2. Otherwise, fetch the release catalog from `Repository` (defaults to `eugeneware/ffmpeg-static`) and use the matching asset.

The archive format is auto-detected from the downloaded content by inspecting magic bytes, so the same `PlatformSource` entry accepts gzip, xz, bzip2, zip, 7z, rar, tar.gz, tar.xz, tar.bz2, plain tar, or an uncompressed binary. For multi-file archives, the entry whose filename matches `ffmpeg`/`ffmpeg.exe` (or the ffprobe equivalent) is extracted; the archive is discarded after extraction.

### Example — private mirror for Linux64

```csharp
var settings = new FetcherSettings
{
    SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
    {
        [SupportedPlatform.Linux64] = new PlatformSource
        {
            FFMpegUrl  = new Uri("https://cdn.example.com/ffmpeg/ffmpeg-linux-x64.gz"),
            FFProbeUrl = new Uri("https://cdn.example.com/ffmpeg/ffprobe-linux-x64.gz")
        }
    }
};

var binaries = await FFMpegStaticFetcher.DownloadBinaries(settings: settings);
```

### Example — zip archive (BtbN/FFmpeg-Builds style)

```csharp
// A zip that contains `ffmpeg.exe` and `ffprobe.exe` somewhere inside — the library
// finds them by filename and extracts just those.
var settings = new FetcherSettings
{
    SourceOverrides = new Dictionary<SupportedPlatform, PlatformSource>
    {
        [SupportedPlatform.Windows64] = new PlatformSource
        {
            FFMpegUrl  = new Uri("https://cdn.example.com/ffmpeg-win64.zip"),
            FFProbeUrl = new Uri("https://cdn.example.com/ffmpeg-win64.zip")
        }
    }
};
```

### Example — a different GitHub fork

```csharp
// Any fork that follows the same `{binary}-{os}-{arch}.gz` asset naming will work.
var settings = new FetcherSettings { Repository = "myorg/ffmpeg-static-mirror" };

var binaries = await FFMpegStaticFetcher.DownloadBinaries(settings: settings);
```

Partial overrides are allowed — if `FFMpegUrl` is set but `FFProbeUrl` is `null`, `ffmpeg` is fetched from the mirror while `ffprobe` still comes from the release catalog.

> **`SourceOverrides` URLs must use `https`.** A non-`https` override URL is rejected with an `FFMpegStaticFetcherException`, since the downloaded executable would otherwise be replaceable in transit. See [Security](#security).

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

When no `platformOverride` is specified, the library auto-detects the current OS and architecture using `System.Runtime.InteropServices.RuntimeInformation`.

> **Windows on ARM64:** the upstream `eugeneware/ffmpeg-static` project does not publish a native `win32-arm64` build, so auto-detection falls back to `Windows64` (x86_64) — which runs under the Windows on ARM x64 emulation layer.

## Available binaries

| Enum value | Binary |
|---|---|
| `FFMpeg` | `ffmpeg` / `ffmpeg.exe` |
| `FFProbe` | `ffprobe` / `ffprobe.exe` |

`FFMpegBinary` is a `[Flags]` enum. The default downloads both.

## How it works

1. Resolves the current platform to an `{os}-{arch}` identifier
2. If `mode: FetchMode.Reload`, wipes the output folder
3. If `systemBinaries` isn't `Ignore`, searches the current working directory and `PATH` for an existing `ffmpeg`/`ffprobe`. `Prefer` returns that path when found (no download); `PreferIfCurrent`/`PreferIfProvenCurrent` additionally run `ffmpeg -version` and keep the system binary only while it's at least as new as the source release
4. For each remaining binary, checks whether the target file already exists in the output folder — if so, reuses it and skips the download. Under `FetchMode.ReFetchIfNewer` it first compares the source's current release tag against the one recorded in `.ffmpeg-fetch.json` and re-downloads when they differ
5. Otherwise resolves the download URL — a `FetcherSettings.SourceOverrides` entry if present, else the matching asset from the GitHub release catalog (default repo `eugeneware/ffmpeg-static`, or `FetcherSettings.Repository`)
6. Streams the response to a temp file (rejecting an empty body), inspects magic bytes to auto-detect the archive format, extracts/decompresses the binary into the output folder via a `.downloading` staging file that is atomically moved into place, and deletes the temp file
7. Appends `.exe` on Windows; sets executable permissions (`755`) on Unix; records the release tag in `.ffmpeg-fetch.json` for future `ReFetchIfNewer` calls

Network and extraction failures (HTTP errors, an empty or corrupt download, a missing archive entry, a rejected non-`https` override) all surface as `FFMpegStaticFetcherException`, whose `Detail` property carries extra diagnostic context.

## Using with wrapper libraries (FFMpegCore)

The output paths plug directly into popular FFmpeg wrappers. Example with [FFMpegCore](https://github.com/rosenbjerg/FFMpegCore) — point its global options at the folder this library wrote to and use the fluent API as usual:

```csharp
using FFMpeg.StaticFetcher;
using FFMpegCore;

var binaries = await FFMpegStaticFetcher.DownloadBinaries();

GlobalFFOptions.Configure(new FFOptions
{
    BinaryFolder = Path.GetDirectoryName(binaries.FFMpegPath!)!
});

await FFMpegArguments
    .FromFileInput("input.mp4")
    .OutputToFile("output.mp3", overwrite: true, options => options
        .WithAudioCodec("libmp3lame")
        .DisableChannel(Channel.Video))
    .ProcessAsynchronously();
```

The same pattern works for other wrappers — any library that takes an `ffmpeg` path (or folder) as a string can be fed `binaries.FFMpegPath` / `Path.GetDirectoryName(binaries.FFMpegPath)`.

## Binary source

All binaries are sourced from [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static), which repackages official FFmpeg builds from:

- **Windows** — [gyan.dev](https://www.gyan.dev/ffmpeg/builds/)
- **Linux** — [johnvansickle.com](https://johnvansickle.com/ffmpeg/)
- **macOS Intel** — [evermeet.cx](https://evermeet.cx/pub/ffmpeg/)
- **macOS Apple Silicon** — [osxexperts.net](https://osxexperts.net/)

## Security

This library downloads native executables and runs them (and hands their paths to you to run). A few things worth knowing for your threat model:

- **Downloaded binaries are not checksum- or signature-verified.** Trust rests on TLS plus the integrity of the source (GitHub releases by default). This matches how comparable binary-fetcher tools work, but if you need stronger guarantees, verify the returned binary against a known hash out-of-band before executing it.
- **Custom source URLs must use `https`.** `FetcherSettings.SourceOverrides` URLs are rejected unless their scheme is `https`, so a plaintext mirror can't be transparently swapped on a hostile network. The default GitHub endpoints are hardcoded `https`.
- **The `PreferIfCurrent` / `PreferIfProvenCurrent` policies execute a discovered binary.** They locate `ffmpeg` by checking the **current working directory first, then `PATH`**, and run it (`ffmpeg -version`, 5-second timeout). Don't enable these policies when the working directory is writable by a less-trusted party, or a planted `./ffmpeg` would run in preference to the system install. The default policy (`Ignore`) never touches the machine.
- **Archive extraction targets a fixed path.** Entries are matched by filename only and written to the output folder, so a malicious archive entry path cannot escape it (no zip-slip); symlink/hardlink entries are skipped in favor of the real file. `SharpCompress` is kept current (the older [GHSA-6c8g-7p36-r338](https://github.com/advisories/GHSA-6c8g-7p36-r338) advisory — which concerned `WriteToDirectory`, not used here — no longer applies to the pinned version).
- **Downloads and decompression are size-bounded** by `FetcherSettings.MaxDownloadBytes` (default 2 GiB), so a hostile mirror can't serve a decompression bomb that fills the disk.

## FFmpeg licensing note

The downloaded FFmpeg/FFProbe binaries are licensed under [GPL v2+](https://www.ffmpeg.org/legal.html) when built with components like x264 or x265 (which the [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static) builds include).

**What this means for your project:**

- **Using FFmpeg** (executing it as a separate process) does **not** make your application a derivative work. GPL imposes no obligations on software that merely runs a GPL binary. You can freely use the downloaded binaries in commercial and proprietary applications.
- **GPL obligations are triggered only by distribution.** If you redistribute the FFmpeg binaries themselves (e.g. bundle them in an installer), you must comply with the GPL — provide the source code, include the license text, and release any modifications under the GPL.
- **This library downloads binaries at runtime**, so your application does not bundle or redistribute FFmpeg. The end user's machine fetches the binaries directly from GitHub.

For full details, see the [FFmpeg Legal page](https://www.ffmpeg.org/legal.html) and the [GPL FAQ](https://www.gnu.org/licenses/gpl-faq.html).

## Testing

```bash
dotnet test
```

> **Note:** the live-network integration tests are tagged `[Trait("Category", "Integration")]` — they download real FFMpeg/FFProbe binaries from GitHub and can take several minutes (and are subject to GitHub's anonymous API rate limit). Run only the fast, offline suite with:
>
> ```bash
> dotnet test --filter "Category!=Integration"
> ```
>
> …and the integration suite explicitly with `--filter "Category=Integration"`. CI runs the offline suite as the gate and the integration suite as a separate non-blocking lane.

The test suite includes:

- **Unit tests** — `PlatformIdentifier` resolution, `EnumExtensions` flag parsing, `FFMpegStaticFetcherException` constructors, `GitHubRelease` JSON deserialization
- **Archive-extractor tests** — auto-detection for gzip, bzip2, zip, tar, tar.gz, tar.bz2, rar, and raw binaries; missing-entry errors; overwriting existing files
- **Mock tests** — HTTP-mocked `DownloadBinariesCore` covering error paths (asset not found, HTTP failure, deserialization failure), platform-specific file naming (`.exe` on Windows, no extension on Linux), URL routing (latest vs. pinned version), reuse semantics (`FetchMode.ReuseExisting` skips HTTP when files are present; `FetchMode.Reload` wipes the folder; `FetchMode.ReFetchIfNewer` reuses when the recorded tag matches and re-downloads when it differs; `SystemBinaryPolicy.Prefer` resolves from PATH; `PreferIfCurrent`/`PreferIfProvenCurrent` compare the local version against the source and keep, download, or fall back on an unknown version via an injected version resolver), and `FetcherSettings` source overrides (full override bypasses GitHub, partial override falls back, raw binaries, custom repository URL, `https`-only enforcement, empty-download rejection)
- **Version-parsing tests** — `ParseVersionToken`/`ParseTagVersion` against real vendor banners and git/nightly builds
- **Process-execution tests** — the real `ResolveLocalVersion` (spawn + stdout parse + the 5-second timeout/kill branch) driven against a `FakeFFmpeg` stub executable, so the version-policy subprocess path is covered without a real ffmpeg on the box
- **Integration tests** (`[Trait("Category", "Integration")]`) — end-to-end downloads from GitHub for single/both binaries, specific versions, platform overrides, `FetchMode.Reload` cleanup, and folder creation

All downloaded files are automatically cleaned up after each test.

### Coverage

Offline test run (`dotnet test --filter "Category!=Integration" --collect:"XPlat Code Coverage"`):

| Metric | Coverage |
|---|---|
| Line | 96.1% |
| Branch | 91.0% |
| Method | 100% (60/60) |

`PlatformIdentifier` is covered across every OS/architecture combination by injecting a fake `IRuntimeProbe`, and the real `ResolveLocalVersion` subprocess path (spawn, stdout parse, 5-second timeout/kill) is exercised against the `FakeFFmpeg` stub. The remaining uncovered lines are the `XZ` decompression path (SharpCompress ships no xz writer, so no in-test fixture is produced) and the platform-specific `chmod 755` branch of `DownloadExtractAndChmodAsync` — the numbers above are from a Windows run, so the Unix-only branch shows as uncovered here and is exercised on the Linux CI lane instead.

## Requirements

- .NET 8.0 or later

## Dependencies

| Package | Version | License | Purpose |
|---|---|---|---|
| [SharpCompress](https://github.com/adamhathcock/sharpcompress) | [![NuGet](https://img.shields.io/nuget/v/SharpCompress.svg?label=&style=flat-square)](https://www.nuget.org/packages/SharpCompress) | [MIT](https://github.com/adamhathcock/sharpcompress/blob/master/LICENSE.txt) | Auto-detects and extracts zip, 7z, rar, tar, tar.gz, tar.xz, tar.bz2, bzip2, xz archives |

The **entire** dependency graph (direct and transitive) is a single MIT-licensed package: **SharpCompress**. Gzip is decompressed using the BCL's built-in `System.IO.Compression.GZipStream`, so the default `eugeneware/ffmpeg-static` path goes through the framework rather than SharpCompress. SourceLink (source-debugging metadata) is provided by the .NET 8+ SDK — no explicit package reference is needed.

## Dependency flow

Dependency updates are fully automated:

1. **Dependabot** (`.github/dependabot.yml`) runs on a **monthly** schedule. Minor + patch NuGet updates are grouped into a single PR; major bumps land as individual PRs so they can be reviewed deliberately. GitHub Actions versions use the same cadence.
2. **Auto-merge** (`.github/workflows/dependabot-auto-merge.yml`) enables GitHub's auto-merge on every Dependabot NuGet PR. Once CI passes, the PR squash-merges to `main` with no human click required. (Auto-merge must be enabled in the repo's settings.)
3. **Release** (`.github/workflows/release.yml`) runs on every push to `main`:
   - A Dependabot NuGet merge (commit author = `dependabot[bot]` **and** the diff touches a `.csproj`) → **minor** version bump.
   - Anything else (regular code change) → **patch** version bump.
   - Manual `workflow_dispatch` still accepts `patch` / `minor` / `major`.

Net effect: a dependency refresh ships as a minor release automatically; day-to-day code changes ship as patches; breaking-change releases are still manual via `workflow_dispatch` with `bump: major`.

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for notable changes, including breaking changes and migration notes between versions.

## License

The source code in this repository is licensed under the [MIT License](LICENSE.md).

The project icon (`icon.png`) is a separate third-party asset and is **not** covered by the MIT license — it is distributed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). See [Credits](#credits) for attribution.

The FFmpeg/FFProbe binaries downloaded at runtime are **not** distributed as part of this project and retain their own licenses — see the [FFmpeg licensing note](#ffmpeg-licensing-note) above.

## Credits

- **Binaries** — downloaded from [eugeneware/ffmpeg-static](https://github.com/eugeneware/ffmpeg-static) GitHub releases.
- **Inspiration** — [FFMpegCore.Extensions.Downloader](https://github.com/rosenbjerg/FFMpegCore).
- **Icon** — the [Jellyfin FFmpeg icon](https://commons.wikimedia.org/wiki/File:Jellyfin_-_icon-transparent-ffmpeg.svg) by the [Jellyfin project](https://github.com/jellyfin/jellyfin-ux), licensed under [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/). Used unmodified.

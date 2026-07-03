# FFMpeg.StaticFetcher

A small .NET 8 library that downloads static **FFmpeg** and **FFprobe** binaries for the current OS and architecture — auto-detecting the archive format and leaving ready-to-run executables in your output folder.

## Install

```
dotnet add package FFMpeg.StaticFetcher
```

## Usage

```csharp
using FFMpeg.StaticFetcher;

// Download the latest ffmpeg + ffprobe to ./ffmpeg.
// Reused on later calls — no re-download when they're already present.
var binaries = await FFMpegStaticFetcher.DownloadBinaries();

// binaries.FFMpegPath / binaries.FFProbePath are absolute paths, ready to run.
```

All parameters are optional:

```csharp
// Just ffmpeg, into a custom folder:
await FFMpegStaticFetcher.DownloadBinaries(binaries: FFMpegBinary.FFMpeg, outputFolder: "./bin");

// Pin a specific release:
await FFMpegStaticFetcher.DownloadBinaries(version: "b6.1.1");

// Reuse what's on disk, but re-download when a newer build ships:
await FFMpegStaticFetcher.DownloadBinaries(mode: FetchMode.ReFetchIfNewer);

// Use an already-installed ffmpeg if present, otherwise download:
await FFMpegStaticFetcher.DownloadBinaries(systemBinaries: SystemBinaryPolicy.Prefer);
```

## Highlights

- Auto-detects OS/arch (Windows, Linux, macOS — x64/x86/arm/arm64) and the archive format (gzip, xz, bzip2, zip, 7z, rar, their `tar.*` variants, or a raw binary).
- Reuse-if-present by default; opt into "re-fetch if newer" or a full reload.
- Optionally prefer a machine-installed `ffmpeg`, including version-aware policies.
- Point at a different repository or a private mirror via `FetcherSettings`.
- Secure by default: `https`-only overrides, size-bounded downloads, no zip-slip.
- One dependency (SharpCompress).

## Documentation

Full docs — examples, configuration, and security notes — are on GitHub:
**https://github.com/Alpaq92/FFMpeg.StaticFetcher**

## License

MIT (this library). The downloaded FFmpeg/FFprobe binaries are GPL-licensed and fetched at runtime — they are **not** redistributed by this package. See the repository for details.

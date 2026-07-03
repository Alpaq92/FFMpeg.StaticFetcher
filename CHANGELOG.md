# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_Nothing yet._

## [1.0.0] - 2026-07-03

First stable release. (Versions `0.0.1`–`0.0.3` were unintended preview publishes of the same
code; they remain available but unsupported — `1.0.0` is the first supported release.)

### Breaking

- **Replaced the `reload` boolean parameter of `DownloadBinaries` with `mode` (`FetchMode`).**
  Migrate `reload: true` → `mode: FetchMode.Reload`. The default (`FetchMode.ReuseExisting`) preserves
  the previous reuse-if-present behavior.
- **Replaced the `useSystemBinaries` boolean parameter with `systemBinaries` (`SystemBinaryPolicy`).**
  Migrate `useSystemBinaries: true` → `systemBinaries: SystemBinaryPolicy.Prefer`. The default
  (`SystemBinaryPolicy.Ignore`) never inspects the machine.
- **`FetcherSettings.SourceOverrides` URLs must now use `https`.** A non-`https` override URL is
  rejected with an `FFMpegStaticFetcherException` instead of being fetched in cleartext.

### Added

- **`FetchMode.ReFetchIfNewer`** — reuses binaries on disk only while they match the source's current
  release tag, re-downloading when a newer release is published. The resolved tag is recorded in a
  `.ffmpeg-fetch.json` marker file in the output folder.
- **Version-aware system-binary policies** — `SystemBinaryPolicy.PreferIfCurrent` and
  `PreferIfProvenCurrent` use an installed `ffmpeg` only while it is at least as new as the source
  release (read via `ffmpeg -version`), differing only in how they treat an undeterminable local
  version (keep vs. download).
- XML documentation is now generated and shipped in the NuGet package, so the public API surfaces in
  consumer IntelliSense.
- A **Security** section in the README documenting the trust model (no checksum verification),
  the `https`-only override rule, and the working-directory execution caveat of the version policies.
- `FetcherSettings.GitHubToken` — sent as a `Bearer` token on `api.github.com` release-metadata requests only
  (never on downloads), raising the anonymous 60 req/hr rate limit. Falls back to the `GITHUB_TOKEN` environment
  variable when unset.
- `FetcherSettings.MaxDownloadBytes` — a ceiling on bytes downloaded/decompressed per binary (default 2 GiB) that
  guards against a hostile mirror serving a decompression bomb.

### Changed

- Download, decompression, and extraction failures (HTTP errors, corrupt archives, missing entries)
  are now normalized to **`FFMpegStaticFetcherException`** — callers can catch a single exception type.
  Cancellation (`OperationCanceledException`) continues to propagate unwrapped.

### Fixed

- **Returned paths are now absolute** for downloaded binaries (the output folder is resolved to a full path),
  matching the documented contract and the system-binary branch — previously a relative `outputFolder` (including
  the default `./ffmpeg`) yielded a relative path that broke if the working directory changed.
- **The `.ffmpeg-fetch.json` tag is only recorded when every requested binary was freshly downloaded from the
  release catalog.** A reused, `SourceOverrides`-sourced, or system-resolved binary now leaves the tag unwritten,
  so a later `ReFetchIfNewer` can't mistake a stale or off-catalog binary for the current release and skip its
  upgrade. (Previously a release fetched only to compare versions could cause a misleading tag to be recorded.)
- **Multi-file archives no longer extract a same-named symlink** over the real binary, and a structurally-valid
  archive that would yield a **0-byte binary** is now rejected.
- **Decompression-bomb guard**: downloads and decompression are bounded by `MaxDownloadBytes`.
- **Insecure `SourceOverrides` URLs fail fast, before any side effect** — a non-`https` (or relative) override URL
  is now validated up front, ahead of the `FetchMode.Reload` folder wipe, any network request, and any disk write.
  A misconfigured override can no longer cost the caller their existing binaries: it is rejected with the folder
  and its contents left intact.
- **Hardened the GitHub API request**: the `repository` is validated against `owner/repo` and the release tag is
  percent-encoded, so a value with `..`, `#`, or `?` can no longer reshape the request path.
- An empty (0-byte) `2xx` response is now rejected instead of being written through as a bogus binary.
- A **0-byte binary already on disk** (e.g. left by a prior interrupted write) is no longer trusted as reusable —
  it is treated as missing and re-downloaded, consistent with the empty-download guard.
- `ResolveLocalVersion` now drains the process's stderr too, so a chatty-stderr build can't fill the pipe and
  spuriously hit the 5-second timeout; the abandoned read on the timeout path is observed.
- The `User-Agent` now reflects this library's version rather than the host application's entry assembly.
- Cleanup of temporary/staging files is best-effort, so a locked file can no longer mask the original
  error.
- Corrected the documented precedence for system binaries: a system binary takes precedence over a
  copy already present in the output folder.

### Dependencies

- **SharpCompress `0.47.4` → `0.49.1`**, clearing the NU1902 / [GHSA-6c8g-7p36-r338](https://github.com/advisories/GHSA-6c8g-7p36-r338) advisory. Also bumped `Microsoft.NET.Test.Sdk`, `coverlet.collector`, and `xunit.runner.visualstudio` to their latest versions.
- **Removed the explicit `Microsoft.SourceLink.GitHub` reference** — SourceLink is provided by the .NET 8+ SDK, so the package was redundant. The library's entire dependency graph is now a single package (SharpCompress).

### Tests / CI

- The live-network integration tests are tagged `[Trait("Category", "Integration")]`. CI now runs the
  fast offline suite (`--filter "Category!=Integration"`) as the gate and the integration suite as a
  separate non-blocking lane (with `GITHUB_TOKEN` for a higher API rate limit).
- The real `ResolveLocalVersion` process path (spawn + stdout parse + 5-second timeout/kill) is now
  exercised against a `FakeFFmpeg` stub executable; method coverage of the library is 100%.
- Test scaffolding cleanup: process-global environment mutation (`PATH`, `FAKE_FFMPEG_*`) is centralized
  behind an `EnvScope` disposable, replacing ~13 repeated save/set/restore `try`/`finally` blocks.

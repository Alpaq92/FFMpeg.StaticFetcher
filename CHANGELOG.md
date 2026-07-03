# Changelog

## [1.0.0](https://github.com/Alpaq92/FFMpeg.StaticFetcher/compare/v0.0.3...v1.0.0) (2026-07-03)


### ⚠ BREAKING CHANGES

* **DownloadBinaries:** replace the `reload` boolean with `mode` (`FetchMode`) — migrate `reload: true` → `mode: FetchMode.Reload`; the default `ReuseExisting` preserves the reuse-if-present behavior
* **DownloadBinaries:** replace `useSystemBinaries` with `systemBinaries` (`SystemBinaryPolicy`) — migrate `useSystemBinaries: true` → `systemBinaries: SystemBinaryPolicy.Prefer`; the default `Ignore` never inspects the machine
* **SourceOverrides:** override URLs must now use `https` — a non-`https` URL is rejected with an `FFMpegStaticFetcherException` instead of being fetched in cleartext


### Features

* reuse binaries on disk by default; add `FetchMode.ReFetchIfNewer` (re-download only when the source's release tag differs from the one recorded in `.ffmpeg-fetch.json`) and `FetchMode.Reload`
* version-aware system-binary policies `PreferIfCurrent` / `PreferIfProvenCurrent` that keep an installed `ffmpeg` only while it is at least as new as the source release
* **settings:** `FetcherSettings.GitHubToken`, sent as a `Bearer` token on `api.github.com` metadata requests only; falls back to the `GITHUB_TOKEN` environment variable
* **settings:** `FetcherSettings.MaxDownloadBytes`, a per-binary download/decompression ceiling (default 2 GiB) that guards against a decompression bomb
* normalize download, decompression, and extraction failures to a single `FFMpegStaticFetcherException` (cancellation still propagates unwrapped)
* generate and ship XML documentation in the package for consumer IntelliSense


### Bug Fixes

* return absolute paths for downloaded binaries even when `outputFolder` is relative
* record the `.ffmpeg-fetch.json` tag only when every binary was freshly downloaded from the release catalog, so `ReFetchIfNewer` can't mistake a reused, override-sourced, or system binary for the current release and skip an upgrade
* validate insecure `SourceOverrides` URLs up front — before the `Reload` folder wipe or any network/disk write — so a misconfigured override can't destroy existing binaries
* reject empty (0-byte) downloads and 0-byte on-disk binaries, and skip a same-named symlink entry in multi-file archives in favor of the real file
* harden the GitHub API request — validate `repository` as `owner/repo` and percent-encode the release tag
* drain stderr in `ResolveLocalVersion` so a chatty build can't fill the pipe and spuriously hit the 5-second timeout
* set the `User-Agent` to this library's version, and make staging-file cleanup best-effort so a locked file can't mask the original error
* **deps:** bump SharpCompress `0.47.4` → `0.49.1`, clearing advisory [GHSA-6c8g-7p36-r338](https://github.com/advisories/GHSA-6c8g-7p36-r338); the entire dependency graph is now a single package
* **deps:** drop the explicit `Microsoft.SourceLink.GitHub` reference (SourceLink ships with the .NET 8+ SDK)

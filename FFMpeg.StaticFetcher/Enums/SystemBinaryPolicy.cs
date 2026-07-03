namespace FFMpeg.StaticFetcher.Enums;

/// <summary>
/// Controls whether (and how) a machine-installed <c>ffmpeg</c>/<c>ffprobe</c> — found in the current
/// working directory or on <c>PATH</c> — is used instead of downloading a managed copy.
/// </summary>
public enum SystemBinaryPolicy
{
    /// <summary>
    /// Never look at the machine. Always resolve from the output folder / download flow. This is the default.
    /// </summary>
    Ignore,

    /// <summary>
    /// Use a system-installed binary when one is present, skipping the download entirely. No version check —
    /// whatever is installed is used as-is. (Equivalent to the former <c>useSystemBinaries: true</c>.)
    /// </summary>
    Prefer,

    /// <summary>
    /// Use the system binary while it is at least as new as the source's current release; download the managed
    /// build when the system binary is older. When the version cannot be determined — a git/nightly build whose
    /// <c>-version</c> output has no semantic version, or a direct-URL source that carries no release tag — the
    /// system binary is <b>kept</b> (the conservative choice: no surprise downloads).
    /// </summary>
    PreferIfCurrent,

    /// <summary>
    /// Like <see cref="PreferIfCurrent"/>, but resolves an undeterminable version the other way: when the system
    /// binary's version cannot be proven at least as new as the source, the managed build is <b>downloaded</b>
    /// rather than trusting an unverifiable local binary.
    /// </summary>
    PreferIfProvenCurrent
}

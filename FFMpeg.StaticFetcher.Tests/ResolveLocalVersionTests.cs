namespace FFMpeg.StaticFetcher.Tests;

// Exercises the REAL FFMpegStaticFetcher.ResolveLocalVersion — process spawn, async stdout read,
// the 5s WaitForExit/kill branch, and the catch-all — by pointing it at the FakeFFmpeg stub
// executable (built and copied next to these tests). The mock policy tests inject a fake resolver
// and never reach this code, so this class is the only coverage of the process glue.
public class ResolveLocalVersionTests
{
    private static readonly string FakeFFmpeg = Path.Combine(
        AppContext.BaseDirectory,
        OperatingSystem.IsWindows() ? "FakeFFmpeg.exe" : "FakeFFmpeg");

    static ResolveLocalVersionTests()
    {
        // The MSBuild copy of the apphost may not carry the executable bit on Unix; ensure it can run.
        if (!OperatingSystem.IsWindows() && File.Exists(FakeFFmpeg))
            File.SetUnixFileMode(FakeFFmpeg,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private static string Stub()
    {
        Assert.True(File.Exists(FakeFFmpeg), $"FakeFFmpeg stub not found at {FakeFFmpeg}");
        return FakeFFmpeg;
    }

    [Fact]
    public void ResolveLocalVersion_ParsesVersionFromRealProcessStdout()
    {
        using var _ = EnvScope.Set("FAKE_FFMPEG_BANNER", "ffmpeg version 6.1.1-fake Copyright (c) the FFmpeg developers");

        Assert.Equal(new Version(6, 1, 1), FFMpegStaticFetcher.ResolveLocalVersion(Stub()));
    }

    [Fact]
    public void ResolveLocalVersion_GitNightlyBanner_ReturnsNull()
    {
        using var _ = EnvScope.Set("FAKE_FFMPEG_BANNER", "ffmpeg version N-113802-g3f1c8b2a9 Copyright (c) the FFmpeg developers");

        Assert.Null(FFMpegStaticFetcher.ResolveLocalVersion(Stub()));
    }

    [Fact]
    public void ResolveLocalVersion_ProcessExceedsTimeout_KillsAndReturnsNull()
    {
        // Sleep past the 5s WaitForExit so the timeout/Kill branch is taken.
        using var _ = EnvScope.Set("FAKE_FFMPEG_SLEEP_MS", "8000");
        using var __ = EnvScope.Set("FAKE_FFMPEG_BANNER", "ffmpeg version 9.9.9 should-not-be-read");

        Assert.Null(FFMpegStaticFetcher.ResolveLocalVersion(Stub()));
    }

    [Fact]
    public void ResolveLocalVersion_NonexistentExecutable_ReturnsNull()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, "definitely-not-here",
            OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");

        Assert.Null(FFMpegStaticFetcher.ResolveLocalVersion(missing));
    }
}

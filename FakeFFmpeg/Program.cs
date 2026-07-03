// Test stand-in for a system ffmpeg. Behaviour is driven by environment variables the test sets
// before spawning this process — the real FFMpegStaticFetcher.ResolveLocalVersion always invokes it
// with a fixed "-version" argument, so the test parameters can't be passed as command-line args.
using System.Globalization;

var sleepMs = Environment.GetEnvironmentVariable("FAKE_FFMPEG_SLEEP_MS");
if (int.TryParse(sleepMs, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms) && ms > 0)
    Thread.Sleep(ms);

var banner = Environment.GetEnvironmentVariable("FAKE_FFMPEG_BANNER")
             ?? "ffmpeg version 6.1.1-fake Copyright (c) the FFmpeg developers";

Console.WriteLine(banner);
return 0;

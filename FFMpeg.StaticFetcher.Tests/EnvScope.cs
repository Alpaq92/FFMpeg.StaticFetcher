namespace FFMpeg.StaticFetcher.Tests;

// Snapshots a process-global environment variable and restores it on Dispose, so a test can mutate
// PATH / FAKE_FFMPEG_* without leaking the change. Use as `using var _ = EnvScope.Set(...)`.
internal sealed class EnvScope : IDisposable
{
    private readonly string _name;
    private readonly string? _original;

    private EnvScope(string name)
    {
        _name = name;
        _original = Environment.GetEnvironmentVariable(name);
    }

    public static EnvScope Set(string name, string? value)
    {
        var scope = new EnvScope(name);
        Environment.SetEnvironmentVariable(name, value);
        return scope;
    }

    // Prepends a directory to a path-list variable (e.g. PATH), preserving the existing entries.
    public static EnvScope Prepend(string name, string value)
    {
        var scope = new EnvScope(name);
        Environment.SetEnvironmentVariable(name, value + Path.PathSeparator + scope._original);
        return scope;
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _original);
}

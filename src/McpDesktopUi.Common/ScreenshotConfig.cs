namespace McpDesktopUi.Common;

/// <summary>
/// Thread-safe screenshot directory configuration shared across all platforms.
/// </summary>
public static class ScreenshotConfig
{
    private static string? _dir;
    private static readonly Lock _lock = new();

    public static string? Dir
    {
        get { lock (_lock) return _dir; }
        set { lock (_lock) _dir = value; }
    }
}

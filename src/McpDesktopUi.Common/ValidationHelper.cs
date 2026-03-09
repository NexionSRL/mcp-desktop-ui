namespace McpDesktopUi.Common;

/// <summary>
/// Shared input validation for MCP tools.
/// </summary>
public static class ValidationHelper
{
    private static readonly HashSet<string> SupportedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "escape", "esc", "enter", "return", "tab", "space"
    };

    /// <summary>
    /// Validates and normalizes a key name. Returns the normalized key or null if unsupported.
    /// </summary>
    public static string? NormalizeKey(string key)
    {
        var lower = key.ToLowerInvariant();
        return lower switch
        {
            "escape" or "esc" => "escape",
            "enter" or "return" => "enter",
            "tab" => "tab",
            "space" => "space",
            _ => null
        };
    }

    public static string UnsupportedKeyError(string key) =>
        $"unsupported key '{key}'. Supported: escape, enter, tab, space";
}

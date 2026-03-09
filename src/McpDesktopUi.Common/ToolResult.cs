namespace McpDesktopUi.Common;

/// <summary>
/// Standardized result formatting for all MCP tools across platforms.
/// </summary>
public static class ToolResult
{
    public static string Ok(string message) => $"OK: {message}";

    public static string Error(string toolName, string message) =>
        $"ERROR: {toolName} failed: {message}";

    public static string Error(string toolName, Exception ex) =>
        Error(toolName, ex.Message);

    /// <summary>
    /// Wraps a tool action with standardized try/catch error handling.
    /// </summary>
    public static string Run(string toolName, Func<string> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            return Error(toolName, ex);
        }
    }
}

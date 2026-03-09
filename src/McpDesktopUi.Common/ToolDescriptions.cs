namespace McpDesktopUi.Common;

/// <summary>
/// Canonical MCP tool descriptions shared across all platforms.
/// Ensures consistent descriptions regardless of platform-specific implementation details.
/// </summary>
public static class ToolDescriptions
{
    public const string Screenshot =
        "Capture a screenshot. If window_title is given, captures only that window; otherwise captures the full screen. Returns PNG as base64, or saves to configured screenshot directory and returns file path.";

    public const string ListWindows =
        "List all visible top-level windows with their titles.";

    public const string GetUiTree =
        "Get the UI element tree for a window. Returns element roles, names, and enabled state. Has a 5-second timeout and max depth of 10 levels.";

    public const string ClickElement =
        "Click a UI element by name inside a window. Finds the element via the accessibility API and performs a click action, with fallback to clicking at element center.";

    public const string ClickAt =
        "Click at specific screen coordinates (x, y).";

    public const string RightClickAt =
        "Right-click at specific screen coordinates (x, y).";

    public const string DoubleClickAt =
        "Double-click at specific screen coordinates (x, y).";

    public const string Drag =
        "Drag from one screen coordinate to another. Holds left mouse button at (from_x, from_y), moves to (to_x, to_y), then releases.";

    public const string Scroll =
        "Scroll up or down inside a window. Positive clicks = scroll up, negative = scroll down. Focuses the window first.";

    public const string ScrollAt =
        "Scroll up or down at specific screen coordinates. Positive clicks = scroll up, negative = scroll down.";

    public const string MoveMouse =
        "Move the mouse cursor to specific screen coordinates without clicking.";

    public const string TypeText =
        "Type text into the focused control of a window. Use click_at to focus a text field first.";

    public const string SendKey =
        "Send a key press to a window. Supports: escape, enter, tab, space.";

    public const string FocusWindow =
        "Bring a window to the foreground.";

    public const string GetWindowRect =
        "Get the screen coordinates and dimensions of a window.";

    public const string CloseWindow =
        "Close a window.";
}

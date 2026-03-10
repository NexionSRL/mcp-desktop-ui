namespace McpDesktopUi.Common;

/// <summary>
/// Canonical MCP tool descriptions shared across all platforms.
/// Ensures consistent descriptions regardless of platform-specific implementation details.
/// </summary>
public static class ToolDescriptions
{
    public const string Screenshot =
        "Capture a full-screen screenshot.";

    public const string ClickAt =
        "Click at specific screen coordinates (x, y).";

    public const string RightClickAt =
        "Right-click at specific screen coordinates (x, y).";

    public const string DoubleClickAt =
        "Double-click at specific screen coordinates (x, y).";

    public const string Drag =
        "Drag from one screen coordinate to another. Holds left mouse button at (from_x, from_y), moves to (to_x, to_y), then releases.";

    public const string Scroll =
        "Scroll up or down at the current mouse position. Positive clicks = scroll up, negative = scroll down.";

    public const string MoveMouse =
        "Move the mouse cursor to specific screen coordinates without clicking.";

    public const string TypeText =
        "Type text into whatever currently has focus. Use click_at to focus a text field first.";

    public const string SendKey =
        "Send a key press to whatever currently has focus. Supports: escape, enter, tab, space.";
}

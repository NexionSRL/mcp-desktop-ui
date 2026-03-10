namespace McpDesktopUi.Common;

/// <summary>
/// Canonical MCP tool descriptions shared across all platforms.
/// Ensures consistent descriptions regardless of platform-specific implementation details.
/// </summary>
public static class ToolDescriptions
{
    public const string Screenshot =
        "Capture a full-screen screenshot. IMPORTANT: The pixel coordinates in the returned screenshot map 1:1 to click coordinates. Do NOT scale, divide, or adjust coordinates from the screenshot — use them exactly as they appear.";

    public const string ClickAt =
        "Click at specific screen coordinates (x, y). Coordinates from screenshots map 1:1 — use them exactly as-is, do NOT scale or divide.";

    public const string RightClickAt =
        "Right-click at specific screen coordinates (x, y). Coordinates from screenshots map 1:1 — use them exactly as-is, do NOT scale or divide.";

    public const string DoubleClickAt =
        "Double-click at specific screen coordinates (x, y). Coordinates from screenshots map 1:1 — use them exactly as-is, do NOT scale or divide.";

    public const string Drag =
        "Drag from one screen coordinate to another. Holds left mouse button at (from_x, from_y), moves to (to_x, to_y), then releases. Coordinates from screenshots map 1:1 — use them exactly as-is, do NOT scale or divide.";

    public const string Scroll =
        "Scroll up or down at the current mouse position. Positive clicks = scroll up, negative = scroll down.";

    public const string MoveMouse =
        "Move the mouse cursor to specific screen coordinates without clicking. Coordinates from screenshots map 1:1 — use them exactly as-is, do NOT scale or divide.";

    public const string TypeText =
        "Type text into whatever currently has focus. Use click_at to focus a text field first.";

    public const string SendKey =
        "Send a key press to whatever currently has focus. Supports: escape, enter, tab, space.";
}

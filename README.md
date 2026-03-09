# mcp-desktop-ui

Cross-platform MCP server for desktop UI automation. Provides 16 tools for screenshots, window management, mouse/keyboard control, and UI accessibility tree inspection.

## Projects

- **McpDesktopUi.Common** — Shared interface, MCP setup, and configuration
- **McpDesktopUi.Windows** — Windows implementation using Win32 APIs (P/Invoke, UIAutomation)
- **McpDesktopUi.MacOS** — macOS implementation using CoreGraphics, Accessibility API, and AppleScript

## Requirements

- .NET 10 SDK
- Windows: Windows 10+ (x64)
- macOS: macOS 13+ (Apple Silicon)

## Build

```bash
# macOS
dotnet build src/McpDesktopUi.MacOS/

# Windows
dotnet build src/McpDesktopUi.Windows/
```

## Publish

```bash
# macOS
dotnet publish src/McpDesktopUi.MacOS/ -c Release

# Windows
dotnet publish src/McpDesktopUi.Windows/ -c Release
```

## Configuration

- `--screenshot-dir <path>` — Save screenshots as PNG files in this directory
- `MCP_SCREENSHOT_DIR` env var — Same as above

## Tools

| Tool | Description |
|------|-------------|
| `screenshot` | Capture full screen or specific window |
| `list_windows` | List visible windows |
| `get_ui_tree` | Get UI automation/accessibility tree |
| `click_element` | Click element by name |
| `click_at` | Click at coordinates |
| `right_click_at` | Right-click at coordinates |
| `double_click_at` | Double-click at coordinates |
| `drag` | Drag from one point to another |
| `scroll` | Scroll in a window |
| `scroll_at` | Scroll at coordinates |
| `move_mouse` | Move mouse cursor |
| `type_text` | Type text into a window |
| `send_key` | Send key press (escape, enter, tab, space) |
| `focus_window` | Bring window to foreground |
| `get_window_rect` | Get window position and size |
| `close_window` | Close a window |

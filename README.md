# mcp-desktop-ui

Cross-platform MCP server for desktop UI automation. Provides 16 tools for screenshots, window management, mouse/keyboard control, and UI accessibility tree inspection.

## Projects

- **McpDesktopUi.Common** — Shared interface, MCP setup, and configuration
- **McpDesktopUi.Windows** — Windows implementation using Win32 APIs (P/Invoke, UIAutomation)
- **McpDesktopUi.MacOS** — macOS implementation using CoreGraphics, Accessibility API, and AppleScript
- **McpDesktopUi.Linux** — Linux implementation using X11/XTest P/Invoke, xdotool, and AT-SPI2

## Requirements

- .NET 10 SDK
- Windows: Windows 10+ (x64)
- macOS: macOS 13+ (Apple Silicon)
- Linux: X11 desktop with `xdotool`, `wmctrl`, and optionally `python3-atspi` (for accessibility tree)

## Build

```bash
# macOS
dotnet build src/McpDesktopUi.MacOS/

# Windows
dotnet build src/McpDesktopUi.Windows/

# Linux
dotnet build src/McpDesktopUi.Linux/
```

## Publish

```bash
# macOS
dotnet publish src/McpDesktopUi.MacOS/ -c Release

# Windows
dotnet publish src/McpDesktopUi.Windows/ -c Release

# Linux
dotnet publish src/McpDesktopUi.Linux/ -c Release
```

## Install from Release

Download the latest binary from [Releases](https://github.com/NexionSRL/mcp-desktop-ui/releases):

| Platform | File |
|----------|------|
| macOS (Apple Silicon) | `mcp-desktop-ui-macos-arm64.tar.gz` |
| Windows (x64) | `mcp-desktop-ui-windows-x64.zip` |
| Linux (x64) | `mcp-desktop-ui-linux-x64.tar.gz` |

### macOS

```bash
tar -xzf mcp-desktop-ui-macos-arm64.tar.gz
chmod +x mcp-desktop-ui
sudo mv mcp-desktop-ui /usr/local/bin/
```

### Windows

Extract `mcp-desktop-ui-windows-x64.zip` and place `mcp-desktop-ui.exe` in your PATH.

### Linux

```bash
tar -xzf mcp-desktop-ui-linux-x64.tar.gz
chmod +x mcp-desktop-ui
sudo mv mcp-desktop-ui /usr/local/bin/
```

Install dependencies:

```bash
# Debian/Ubuntu
sudo apt install xdotool wmctrl imagemagick python3-gi gir1.2-atspi-2.0

# Fedora
sudo dnf install xdotool wmctrl ImageMagick python3-gobject at-spi2-core
```

## Release

To create a new release, push a version tag:

```bash
git tag v1.1.0
git push origin v1.1.0
```

The GitHub Action builds both platforms in parallel and creates a release with the binaries attached.

## MCP Client Setup

### Claude Desktop

Add to your `claude_desktop_config.json`:

**macOS** (`~/Library/Application Support/Claude/claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "desktop-ui": {
      "command": "/usr/local/bin/mcp-desktop-ui",
      "args": ["--screenshot-dir", "./tmp/screenshots"]
    }
  }
}
```

**Windows** (`%APPDATA%\Claude\claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "desktop-ui": {
      "command": "C:\\path\\to\\mcp-desktop-ui.exe",
      "args": ["--screenshot-dir", ".\\tmp\\screenshots"]
    }
  }
}
```

### Claude Code (CLI)

Add to your `~/.claude/settings.json` or project `.claude/settings.json`:

```json
{
  "mcpServers": {
    "desktop-ui": {
      "command": "/usr/local/bin/mcp-desktop-ui",
      "args": ["--screenshot-dir", "./tmp/screenshots"]
    }
  }
}
```

### VS Code (Copilot / Continue / etc.)

Add to `.vscode/mcp.json` in your project:

```json
{
  "servers": {
    "desktop-ui": {
      "command": "/usr/local/bin/mcp-desktop-ui",
      "args": ["--screenshot-dir", "./tmp/screenshots"]
    }
  }
}
```

### Cursor

Add to Cursor Settings > MCP Servers, or in `~/.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "desktop-ui": {
      "command": "/usr/local/bin/mcp-desktop-ui",
      "args": ["--screenshot-dir", "./tmp/screenshots"]
    }
  }
}
```

## Permissions

This MCP server controls your mouse, keyboard, and reads the screen. **The application that runs the MCP server needs OS-level permissions**, not the server binary itself.

### macOS

The **host application** (VS Code, Claude Desktop, Terminal, etc.) needs **Accessibility** and **Screen Recording** permissions:

1. Go to **System Settings > Privacy & Security > Accessibility**
2. Add the app that runs the MCP server (e.g., `Visual Studio Code`, `Claude`, `Terminal`, `iTerm2`)
3. Go to **System Settings > Privacy & Security > Screen Recording**
4. Add the same app

> Without Accessibility permission, click/type/UI tree tools will fail. Without Screen Recording, screenshots will be blank or fail.

The server checks for Accessibility permission at startup and logs a warning to stderr if not granted.

### Windows

Some tools may require **running as Administrator** (e.g., interacting with elevated windows). For most applications, standard user permissions are sufficient.

### Linux

A running **X11 display** is required (Wayland is not supported). The server checks at startup that `DISPLAY` is set and that required tools (`xdotool`, `wmctrl`, screenshot tool, `python3`) are installed.

### VS Code / Cursor

When using this MCP server from VS Code or Cursor, **VS Code/Cursor is the host application** that needs the OS permissions above. On macOS, you must add `Visual Studio Code` (or `Cursor`) to both Accessibility and Screen Recording in System Settings.

### Claude Desktop

On macOS, add `Claude` to Accessibility and Screen Recording in System Settings.

### Claude Code (CLI)

The terminal application you use (Terminal, iTerm2, Warp, etc.) needs the permissions. Add your terminal app to Accessibility and Screen Recording.

## Configuration

- `--screenshot-dir <path>` — Override screenshot directory. Default: `./tmp/screenshots`

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

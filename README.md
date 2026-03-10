# mcp-desktop-ui

Cross-platform MCP server for desktop UI automation. Provides 9 tools for screenshots, mouse/keyboard control.

## Projects

- **McpDesktopUi.Common** — Shared interface, MCP setup, and configuration
- **McpDesktopUi.Windows** — Windows implementation using Win32 APIs (P/Invoke)
- **McpDesktopUi.MacOS** — macOS implementation using CoreGraphics
- **McpDesktopUi.Linux** — Linux implementation using X11/XTest P/Invoke and xdotool

## Requirements

- .NET 10 SDK
- Windows: Windows 10+ (x64)
- macOS: macOS 13+ (Apple Silicon)
- Linux: X11 desktop with `xdotool` and `ImageMagick` (for screenshots)

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
sudo apt install xdotool imagemagick

# Fedora
sudo dnf install xdotool ImageMagick
```

## Release

To create a new release, push a version tag:

```bash
git tag v1.4.1
git push origin v1.4.1
```

The GitHub Action builds all platforms in parallel and creates a release with the binaries attached.

## CLI Flags

- `--version` — Print version and exit
- `--screenshot-dir <path>` — Override screenshot directory (default: `./tmp/screenshots`)

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

The server checks for Accessibility permission at startup and logs a warning to stderr if not granted.

### Windows

Some tools may require **running as Administrator** (e.g., interacting with elevated windows). For most applications, standard user permissions are sufficient.

### Linux

A running **X11 display** is required (Wayland is not supported). The server checks at startup that `DISPLAY` is set and that required tools (`xdotool`, screenshot tool) are installed.

## Tools

| Tool | Parameters | Description |
|------|------------|-------------|
| `screenshot` | *(none)* | Capture a full-screen screenshot |
| `click_at` | `x`, `y` | Click at screen coordinates |
| `right_click_at` | `x`, `y` | Right-click at screen coordinates |
| `double_click_at` | `x`, `y` | Double-click at screen coordinates |
| `drag` | `from_x`, `from_y`, `to_x`, `to_y` | Drag from one point to another |
| `scroll` | `clicks` | Scroll at current mouse position (positive=up, negative=down) |
| `move_mouse` | `x`, `y` | Move mouse cursor without clicking |
| `type_text` | `text` | Type text into whatever has focus |
| `send_key` | `key` | Send key press (escape, enter, tab, space) |

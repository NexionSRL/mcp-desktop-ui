using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace LinuxMcp;

[McpServerToolType]
public static class UiTools
{
    public static string? ScreenshotDir { get; set; }

    // ── X11 P/Invoke ───────────────────────────────────────────────────────

    private const string LibX11 = "libX11.so.6";
    private const string LibXtst = "libXtst.so.6";

    [DllImport(LibX11)]
    private static extern IntPtr XOpenDisplay(string? display);

    [DllImport(LibX11)]
    private static extern int XCloseDisplay(IntPtr display);

    [DllImport(LibX11)]
    private static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XFlush(IntPtr display);

    [DllImport(LibX11)]
    private static extern int XWarpPointer(IntPtr display, IntPtr src_w, IntPtr dest_w,
        int src_x, int src_y, uint src_width, uint src_height, int dest_x, int dest_y);

    [DllImport(LibXtst)]
    private static extern int XTestFakeButtonEvent(IntPtr display, uint button, bool is_press, ulong delay);

    [DllImport(LibXtst)]
    private static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, bool is_press, ulong delay);

    [DllImport(LibX11)]
    private static extern uint XKeysymToKeycode(IntPtr display, ulong keysym);

    // X11 keysyms
    private const ulong XK_Escape = 0xff1b;
    private const ulong XK_Return = 0xff0d;
    private const ulong XK_Tab = 0xff09;
    private const ulong XK_space = 0x0020;

    // ── Tools ──────────────────────────────────────────────────────────────

    [McpServerTool, Description("Capture a screenshot. If window_title is given, captures only that window; otherwise captures the full screen. Returns PNG as base64, or saves to configured screenshot directory and returns file path.")]
    public static string screenshot(string? window_title = null)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var label = string.IsNullOrEmpty(window_title) ? "screen" : SanitizeFileName(window_title);
            var tempPath = Path.Combine(Path.GetTempPath(), $"{timestamp}_{label}.png");

            string args;
            if (window_title != null)
            {
                var windowId = FindWindowId(window_title);
                if (windowId == null)
                    return $"ERROR: window '{window_title}' not found";
                // Try import (ImageMagick) first, fall back to scrot
                args = $"-window {windowId} \"{tempPath}\"";
                var result = RunCommand("import", args);
                if (!File.Exists(tempPath))
                {
                    // Fallback: scrot with focused window
                    RunCommand("xdotool", $"windowactivate --sync {windowId}");
                    Thread.Sleep(200);
                    RunCommand("scrot", $"-u \"{tempPath}\"");
                }
            }
            else
            {
                // Try import first, fall back to scrot
                var result = RunCommand("import", $"-window root \"{tempPath}\"");
                if (!File.Exists(tempPath))
                    RunCommand("scrot", $"\"{tempPath}\"");
            }

            if (!File.Exists(tempPath))
                return "ERROR: screenshot failed - ensure 'import' (ImageMagick) or 'scrot' is installed";

            if (!string.IsNullOrEmpty(ScreenshotDir))
            {
                var destPath = Path.Combine(ScreenshotDir, Path.GetFileName(tempPath));
                File.Move(tempPath, destPath, overwrite: true);
                return $"OK: saved screenshot to {destPath}";
            }

            var bytes = File.ReadAllBytes(tempPath);
            File.Delete(tempPath);
            return Convert.ToBase64String(bytes);
        }
        catch (Exception ex)
        {
            return $"ERROR: screenshot failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("List all visible top-level windows with their titles.")]
    public static string list_windows()
    {
        try
        {
            var output = RunCommand("wmctrl", "-l");
            if (string.IsNullOrEmpty(output))
            {
                // Fallback to xdotool
                output = RunCommand("xdotool", "search --onlyvisible --name \"\"");
                if (string.IsNullOrEmpty(output))
                    return "No visible windows found";

                var sb = new StringBuilder();
                int i = 1;
                foreach (var wid in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = RunCommand("xdotool", $"getwindowname {wid}").Trim();
                    if (!string.IsNullOrEmpty(name))
                        sb.AppendLine($"{i++}. {name}");
                }
                return sb.Length == 0 ? "No visible windows found" : sb.ToString().TrimEnd();
            }

            // Parse wmctrl output: ID DESKTOP HOST TITLE
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var result = new StringBuilder();
            int idx = 1;
            foreach (var line in lines)
            {
                var parts = Regex.Match(line, @"^0x[\da-f]+\s+\S+\s+\S+\s+(.+)$", RegexOptions.IgnoreCase);
                if (parts.Success)
                    result.AppendLine($"{idx++}. {parts.Groups[1].Value}");
            }
            return result.Length == 0 ? "No visible windows found" : result.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: list_windows failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the accessibility element tree for a window using AT-SPI2. Returns element roles, names, and states. Has a 5-second timeout and max depth of 10 levels.")]
    public static string get_ui_tree(string window_title)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            // Use xprop to get PID
            var pidOutput = RunCommand("xprop", $"-id {windowId} _NET_WM_PID");
            var pidMatch = Regex.Match(pidOutput, @"=\s*(\d+)");
            if (!pidMatch.Success)
                return $"ERROR: could not get PID for '{window_title}'";

            var pid = pidMatch.Groups[1].Value;

            // Use gdbus to query AT-SPI2 accessibility tree
            var task = Task.Run(() =>
            {
                var output = RunCommand("python3", $"-c \"\nimport subprocess, json\ntry:\n    import gi\n    gi.require_version('Atspi', '2.0')\n    from gi.repository import Atspi\n    desktop = Atspi.get_desktop(0)\n    def walk(el, depth):\n        if depth > 10: return ''\n        indent = '  ' * depth\n        role = el.get_role_name() if el else ''\n        name = el.get_name() if el else ''\n        lines = f'{{indent}}[{{role}}] \\\"{{name}}\\\"\\n'\n        try:\n            for i in range(el.get_child_count()):\n                child = el.get_child_at_index(i)\n                if child: lines += walk(child, depth + 1)\n        except: pass\n        return lines\n    for i in range(desktop.get_child_count()):\n        app = desktop.get_child_at_index(i)\n        if app and str(app.get_process_id()) == '{pid}':\n            print(walk(app, 0))\n            break\n    else:\n        print('(app not found in AT-SPI)')\nexcept Exception as e:\n    print(f'ERROR: {{e}}')\n\"");
                return output;
            });

            if (!task.Wait(TimeSpan.FromSeconds(5)))
                return $"ERROR: timed out reading UI tree for '{window_title}'";

            var result = task.Result.Trim();
            return string.IsNullOrEmpty(result) ? "(empty tree)" : result;
        }
        catch (Exception ex)
        {
            return $"ERROR: get_ui_tree failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Click a UI element by name inside a window. Uses AT-SPI2 to find the element and perform a click action, with fallback to clicking at element center.")]
    public static string click_element(string window_title, string element_name)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            var pidOutput = RunCommand("xprop", $"-id {windowId} _NET_WM_PID");
            var pidMatch = Regex.Match(pidOutput, @"=\s*(\d+)");
            if (!pidMatch.Success)
                return $"ERROR: could not get PID for '{window_title}'";

            var pid = pidMatch.Groups[1].Value;
            var escapedName = element_name.Replace("'", "\\'").Replace("\"", "\\\"");

            var output = RunCommand("python3", $"-c \"\nimport gi\ngi.require_version('Atspi', '2.0')\nfrom gi.repository import Atspi\ndesktop = Atspi.get_desktop(0)\ndef find(el, name, depth):\n    if depth > 10: return None\n    if el.get_name() and '{escapedName}'.lower() in el.get_name().lower():\n        return el\n    try:\n        for i in range(el.get_child_count()):\n            child = el.get_child_at_index(i)\n            if child:\n                r = find(child, name, depth+1)\n                if r: return r\n    except: pass\n    return None\nfor i in range(desktop.get_child_count()):\n    app = desktop.get_child_at_index(i)\n    if app and str(app.get_process_id()) == '{pid}':\n        el = find(app, '{escapedName}', 0)\n        if el:\n            try:\n                act = el.get_action_iface()\n                if act and act.get_n_actions() > 0:\n                    act.do_action(0)\n                    print(f'OK: clicked {{el.get_name()}}')\n                else:\n                    comp = el.get_component_iface()\n                    if comp:\n                        ext = comp.get_extents(Atspi.CoordType.SCREEN)\n                        cx = ext.x + ext.width // 2\n                        cy = ext.y + ext.height // 2\n                        print(f'CLICK:{{cx}},{{cy}}')\n                    else:\n                        print('ERROR: no action or position')\n            except Exception as e:\n                print(f'ERROR: {{e}}')\n        else:\n            print(f'ERROR: element not found')\n        break\nelse:\n    print('ERROR: app not found')\n\"");

            output = output.Trim();
            if (output.StartsWith("CLICK:"))
            {
                var coords = output[6..].Split(',');
                var cx = int.Parse(coords[0]);
                var cy = int.Parse(coords[1]);
                ClickAtPoint(cx, cy);
                return $"OK: clicked '{element_name}' at ({cx},{cy})";
            }
            return output;
        }
        catch (Exception ex)
        {
            return $"ERROR: click_element failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Click at specific screen coordinates (x, y).")]
    public static string click_at(int x, int y)
    {
        try
        {
            ClickAtPoint(x, y);
            return $"OK: clicked at ({x},{y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: click_at failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Right-click at specific screen coordinates (x, y).")]
    public static string right_click_at(int x, int y)
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var root = XDefaultRootWindow(display);
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
                XFlush(display);
                Thread.Sleep(50);
                XTestFakeButtonEvent(display, 3, true, 0);
                XTestFakeButtonEvent(display, 3, false, 50);
                XFlush(display);
                return $"OK: right-clicked at ({x},{y})";
            }
            finally { XCloseDisplay(display); }
        }
        catch (Exception ex)
        {
            return $"ERROR: right_click_at failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Double-click at specific screen coordinates (x, y).")]
    public static string double_click_at(int x, int y)
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var root = XDefaultRootWindow(display);
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
                XFlush(display);
                Thread.Sleep(50);
                XTestFakeButtonEvent(display, 1, true, 0);
                XTestFakeButtonEvent(display, 1, false, 30);
                XFlush(display);
                Thread.Sleep(50);
                XTestFakeButtonEvent(display, 1, true, 0);
                XTestFakeButtonEvent(display, 1, false, 30);
                XFlush(display);
                return $"OK: double-clicked at ({x},{y})";
            }
            finally { XCloseDisplay(display); }
        }
        catch (Exception ex)
        {
            return $"ERROR: double_click_at failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Drag from one screen coordinate to another with smooth movement in 10 steps.")]
    public static string drag(int from_x, int from_y, int to_x, int to_y)
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var root = XDefaultRootWindow(display);

                // Move to start and press
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, from_x, from_y);
                XFlush(display);
                Thread.Sleep(50);
                XTestFakeButtonEvent(display, 1, true, 0);
                XFlush(display);
                Thread.Sleep(100);

                // Smooth drag in 10 steps
                for (int i = 1; i <= 10; i++)
                {
                    double t = (double)i / 10;
                    int cx = from_x + (int)((to_x - from_x) * t);
                    int cy = from_y + (int)((to_y - from_y) * t);
                    XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, cx, cy);
                    XFlush(display);
                    Thread.Sleep(20);
                }

                // Release
                XTestFakeButtonEvent(display, 1, false, 0);
                XFlush(display);
                return $"OK: dragged from ({from_x},{from_y}) to ({to_x},{to_y})";
            }
            finally { XCloseDisplay(display); }
        }
        catch (Exception ex)
        {
            return $"ERROR: drag failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Scroll up or down inside a window. Positive clicks = scroll up, negative = scroll down.")]
    public static string scroll(string window_title, int clicks)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            // Focus window
            RunCommand("xdotool", $"windowactivate --sync {windowId}");
            Thread.Sleep(200);

            // Get window geometry and move mouse to center
            var geom = RunCommand("xdotool", $"getwindowgeometry {windowId}");
            var posMatch = Regex.Match(geom, @"Position:\s+(\d+),(\d+)");
            var sizeMatch = Regex.Match(geom, @"Geometry:\s+(\d+)x(\d+)");
            if (posMatch.Success && sizeMatch.Success)
            {
                int cx = int.Parse(posMatch.Groups[1].Value) + int.Parse(sizeMatch.Groups[1].Value) / 2;
                int cy = int.Parse(posMatch.Groups[2].Value) + int.Parse(sizeMatch.Groups[2].Value) / 2;
                var display = XOpenDisplay(null);
                if (display != IntPtr.Zero)
                {
                    var root = XDefaultRootWindow(display);
                    XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, cx, cy);
                    XFlush(display);
                    XCloseDisplay(display);
                    Thread.Sleep(50);
                }
            }

            // Scroll: button 4 = up, button 5 = down
            uint button = clicks > 0 ? 4u : 5u;
            int count = Math.Abs(clicks);
            var disp = XOpenDisplay(null);
            if (disp == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                for (int i = 0; i < count; i++)
                {
                    XTestFakeButtonEvent(disp, button, true, 0);
                    XTestFakeButtonEvent(disp, button, false, 10);
                    XFlush(disp);
                    Thread.Sleep(30);
                }
            }
            finally { XCloseDisplay(disp); }

            var direction = clicks > 0 ? "up" : "down";
            return $"OK: scrolled {direction} {count} click(s) in '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: scroll failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Scroll up or down at specific screen coordinates. Positive clicks = scroll up, negative = scroll down.")]
    public static string scroll_at(int x, int y, int clicks)
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var root = XDefaultRootWindow(display);
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
                XFlush(display);
                Thread.Sleep(50);

                uint button = clicks > 0 ? 4u : 5u;
                int count = Math.Abs(clicks);
                for (int i = 0; i < count; i++)
                {
                    XTestFakeButtonEvent(display, button, true, 0);
                    XTestFakeButtonEvent(display, button, false, 10);
                    XFlush(display);
                    Thread.Sleep(30);
                }
            }
            finally { XCloseDisplay(display); }

            var direction = clicks > 0 ? "up" : "down";
            return $"OK: scrolled {direction} {Math.Abs(clicks)} click(s) at ({x},{y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: scroll_at failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Move the mouse cursor to specific screen coordinates without clicking.")]
    public static string move_mouse(int x, int y)
    {
        try
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var root = XDefaultRootWindow(display);
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
                XFlush(display);
                return $"OK: moved mouse to ({x},{y})";
            }
            finally { XCloseDisplay(display); }
        }
        catch (Exception ex)
        {
            return $"ERROR: move_mouse failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Type text into a window using xdotool. Use click_at to focus a text field first.")]
    public static string type_text(string window_title, string text)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            RunCommand("xdotool", $"windowactivate --sync {windowId}");
            Thread.Sleep(200);
            RunCommand("xdotool", $"type --delay 12 -- \"{EscapeShellArg(text)}\"");
            return $"OK: typed '{text}' into '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: type_text failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Send a key press to a window. Supports: escape, enter, tab, space.")]
    public static string send_key(string window_title, string key)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            ulong keysym = key.ToLowerInvariant() switch
            {
                "escape" or "esc" => XK_Escape,
                "enter" or "return" => XK_Return,
                "tab" => XK_Tab,
                "space" => XK_space,
                _ => 0
            };

            if (keysym == 0)
                return $"ERROR: unsupported key '{key}'. Supported: escape, enter, tab, space";

            RunCommand("xdotool", $"windowactivate --sync {windowId}");
            Thread.Sleep(100);

            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var keycode = XKeysymToKeycode(display, keysym);
                XTestFakeKeyEvent(display, keycode, true, 0);
                XTestFakeKeyEvent(display, keycode, false, 50);
                XFlush(display);
            }
            finally { XCloseDisplay(display); }

            return $"OK: sent '{key}' to '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: send_key failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Bring a window to the foreground.")]
    public static string focus_window(string window_title)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            RunCommand("xdotool", $"windowactivate --sync {windowId}");
            return $"OK: focused '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: focus_window failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the screen coordinates (x, y, width, height) of a window.")]
    public static string get_window_rect(string window_title)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            var geom = RunCommand("xdotool", $"getwindowgeometry {windowId}");
            var posMatch = Regex.Match(geom, @"Position:\s+(\d+),(\d+)");
            var sizeMatch = Regex.Match(geom, @"Geometry:\s+(\d+)x(\d+)");

            if (!posMatch.Success || !sizeMatch.Success)
                return $"ERROR: could not get window rect for '{window_title}'";

            return $"x={posMatch.Groups[1].Value} y={posMatch.Groups[2].Value} width={sizeMatch.Groups[1].Value} height={sizeMatch.Groups[2].Value}";
        }
        catch (Exception ex)
        {
            return $"ERROR: get_window_rect failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Close a window by sending WM_DELETE_WINDOW via xdotool.")]
    public static string close_window(string window_title)
    {
        try
        {
            var windowId = FindWindowId(window_title);
            if (windowId == null)
                return $"ERROR: window '{window_title}' not found";

            RunCommand("xdotool", $"windowclose {windowId}");
            return $"OK: closed '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: close_window failed: {ex.Message}";
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void ClickAtPoint(int x, int y)
    {
        var display = XOpenDisplay(null);
        if (display == IntPtr.Zero)
            throw new Exception("cannot open X display");
        try
        {
            var root = XDefaultRootWindow(display);
            XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
            XFlush(display);
            Thread.Sleep(50);
            XTestFakeButtonEvent(display, 1, true, 0);
            XTestFakeButtonEvent(display, 1, false, 50);
            XFlush(display);
        }
        finally { XCloseDisplay(display); }
    }

    private static string? FindWindowId(string titleFragment)
    {
        // Try xdotool search by name
        var output = RunCommand("xdotool", $"search --onlyvisible --name \"{EscapeShellArg(titleFragment)}\"");
        var firstId = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (!string.IsNullOrEmpty(firstId))
            return firstId.Trim();

        // Fallback: search all windows via wmctrl
        output = RunCommand("wmctrl", "-l");
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
            {
                var match = Regex.Match(line, @"^(0x[\da-f]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value;
            }
        }

        return null;
    }

    private static string RunCommand(string command, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(command, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return "";
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);
            return output;
        }
        catch
        {
            return "";
        }
    }

    private static string EscapeShellArg(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var result = sb.ToString().Trim();
        return result.Length > 50 ? result[..50] : result;
    }
}

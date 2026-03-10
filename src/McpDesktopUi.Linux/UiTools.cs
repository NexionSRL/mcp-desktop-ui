using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using McpDesktopUi.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace LinuxMcp;

[McpServerToolType]
public static class UiTools
{
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

    [McpServerTool, Description(ToolDescriptions.Screenshot)]
    public static CallToolResult screenshot()
    {
        try
        {
            var tempPath = ScreenshotHelper.GenerateTempPath(null);

            // Try import first, fall back to scrot
            RunCommand("import", $"-window root \"{tempPath}\"");
            if (!File.Exists(tempPath))
                RunCommand("scrot", $"\"{tempPath}\"");

            if (!File.Exists(tempPath))
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "ERROR: screenshot failed - ensure 'import' (ImageMagick) or 'scrot' is installed" }] };

            return ScreenshotHelper.ToImageResultFromFile(tempPath, "image/png", null);
        }
        catch (Exception ex)
        {
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = $"ERROR: {ex.Message}" }] };
        }
    }

    [McpServerTool, Description(ToolDescriptions.ClickAt)]
    public static string click_at(int x, int y)
    {
        return ToolResult.Run("click_at", () =>
        {
            ClickAtPoint(x, y);
            return ToolResult.Ok($"clicked at ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.RightClickAt)]
    public static string right_click_at(int x, int y)
    {
        return ToolResult.Run("right_click_at", () =>
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
                return ToolResult.Ok($"right-clicked at ({x},{y})");
            }
            finally { XCloseDisplay(display); }
        });
    }

    [McpServerTool, Description(ToolDescriptions.DoubleClickAt)]
    public static string double_click_at(int x, int y)
    {
        return ToolResult.Run("double_click_at", () =>
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
                return ToolResult.Ok($"double-clicked at ({x},{y})");
            }
            finally { XCloseDisplay(display); }
        });
    }

    [McpServerTool, Description(ToolDescriptions.Drag)]
    public static string drag(int from_x, int from_y, int to_x, int to_y)
    {
        return ToolResult.Run("drag", () =>
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
                return ToolResult.Ok($"dragged from ({from_x},{from_y}) to ({to_x},{to_y})");
            }
            finally { XCloseDisplay(display); }
        });
    }

    [McpServerTool, Description(ToolDescriptions.Scroll)]
    public static string scroll(int clicks)
    {
        return ToolResult.Run("scroll", () =>
        {
            // Scroll at current mouse position: button 4 = up, button 5 = down
            uint button = clicks > 0 ? 4u : 5u;
            int count = Math.Abs(clicks);
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
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
            return ToolResult.Ok($"scrolled {direction} {count} click(s) at current mouse position");
        });
    }

    [McpServerTool, Description(ToolDescriptions.MoveMouse)]
    public static string move_mouse(int x, int y)
    {
        return ToolResult.Run("move_mouse", () =>
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero) return "ERROR: cannot open X display";
            try
            {
                var root = XDefaultRootWindow(display);
                XWarpPointer(display, IntPtr.Zero, root, 0, 0, 0, 0, x, y);
                XFlush(display);
                return ToolResult.Ok($"moved mouse to ({x},{y})");
            }
            finally { XCloseDisplay(display); }
        });
    }

    [McpServerTool, Description(ToolDescriptions.TypeText)]
    public static string type_text(string text)
    {
        return ToolResult.Run("type_text", () =>
        {
            RunCommand("xdotool", $"type --delay 12 -- \"{EscapeShellArg(text)}\"");
            return ToolResult.Ok($"typed '{text}'");
        });
    }

    [McpServerTool, Description(ToolDescriptions.SendKey)]
    public static string send_key(string key)
    {
        return ToolResult.Run("send_key", () =>
        {
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

            return ToolResult.Ok($"sent '{key}'");
        });
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
}

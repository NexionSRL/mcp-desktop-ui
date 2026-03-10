using System.Diagnostics;
using System.Runtime.InteropServices;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;
using McpDesktopUi.Common;

namespace MacOsMcp;

[McpServerToolType]
public static class UiTools
{

    // ── CoreGraphics P/Invoke ──────────────────────────────────────────────

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    // CGEvent
    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreateMouseEvent(IntPtr source, CGEventType mouseType, CGPoint mouseCursorPosition, CGMouseButton mouseButton);

    [DllImport(CoreGraphics)]
    private static extern void CGEventPost(CGEventTapLocation tap, IntPtr evt);

    [DllImport(CoreGraphics)]
    private static extern void CGEventSetIntegerValueField(IntPtr evt, int field, long value);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreateScrollWheelEvent(IntPtr source, CGScrollEventUnit units, uint wheelCount, int wheel1);

    [DllImport(CoreGraphics)]
    private static extern IntPtr CGEventCreateKeyboardEvent(IntPtr source, ushort virtualKey, bool keyDown);

    [DllImport(CoreGraphics)]
    private static extern void CGEventKeyboardSetUnicodeString(IntPtr evt, int stringLength, ushort[] unicodeString);

    // CoreFoundation
    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr cf);

    private enum CGEventType : uint
    {
        LeftMouseDown = 1,
        LeftMouseUp = 2,
        RightMouseDown = 3,
        RightMouseUp = 4,
        MouseMoved = 5,
        LeftMouseDragged = 6,
        KeyDown = 10,
        KeyUp = 11,
        ScrollWheel = 22,
    }

    private enum CGMouseButton : uint
    {
        Left = 0,
        Right = 1,
    }

    private enum CGEventTapLocation : uint
    {
        HID = 0,
    }

    private enum CGScrollEventUnit : uint
    {
        Line = 1,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
        public CGPoint(double x, double y) { X = x; Y = y; }
    }

    // ── Tools ──────────────────────────────────────────────────────────────

    [McpServerTool, Description(ToolDescriptions.Screenshot)]
    public static CallToolResult screenshot()
    {
        try
        {
            var tempPath = ScreenshotHelper.GenerateTempPath(null, ".jpg");

            var psi = new ProcessStartInfo("screencapture", $"-t jpg \"{tempPath}\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(10000);

            if (proc.ExitCode != 0 || !File.Exists(tempPath))
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = $"ERROR: screencapture failed (exit code {proc.ExitCode})" }] };

            return ScreenshotHelper.ToImageResultFromFile(tempPath, "image/jpeg", null);
        }
        catch (Exception ex)
        {
            return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = $"ERROR: screenshot failed: {ex.Message}" }] };
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
            var point = new CGPoint(x, y);
            var down = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.RightMouseDown, point, CGMouseButton.Right);
            var up = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.RightMouseUp, point, CGMouseButton.Right);
            CGEventPost(CGEventTapLocation.HID, down);
            Thread.Sleep(50);
            CGEventPost(CGEventTapLocation.HID, up);
            CFRelease(down);
            CFRelease(up);
            return ToolResult.Ok($"right-clicked at ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.DoubleClickAt)]
    public static string double_click_at(int x, int y)
    {
        return ToolResult.Run("double_click_at", () =>
        {
            var point = new CGPoint(x, y);
            var down1 = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseDown, point, CGMouseButton.Left);
            var up1 = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseUp, point, CGMouseButton.Left);
            CGEventPost(CGEventTapLocation.HID, down1);
            Thread.Sleep(30);
            CGEventPost(CGEventTapLocation.HID, up1);
            Thread.Sleep(50);

            var down2 = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseDown, point, CGMouseButton.Left);
            var up2 = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseUp, point, CGMouseButton.Left);
            // Set click count to 2 for double-click
            CGEventSetIntegerValueField(down2, 1, 2);
            CGEventSetIntegerValueField(up2, 1, 2);
            CGEventPost(CGEventTapLocation.HID, down2);
            Thread.Sleep(30);
            CGEventPost(CGEventTapLocation.HID, up2);

            CFRelease(down1);
            CFRelease(up1);
            CFRelease(down2);
            CFRelease(up2);
            return ToolResult.Ok($"double-clicked at ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.Drag)]
    public static string drag(int from_x, int from_y, int to_x, int to_y)
    {
        return ToolResult.Run("drag", () =>
        {
            var startPoint = new CGPoint(from_x, from_y);

            // Mouse down at start
            var down = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseDown, startPoint, CGMouseButton.Left);
            CGEventPost(CGEventTapLocation.HID, down);
            CFRelease(down);
            Thread.Sleep(100);

            // Smooth drag in 10 steps
            const int steps = 10;
            for (int i = 1; i <= steps; i++)
            {
                double t = (double)i / steps;
                var point = new CGPoint(
                    from_x + (to_x - from_x) * t,
                    from_y + (to_y - from_y) * t);
                var drag_evt = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseDragged, point, CGMouseButton.Left);
                CGEventPost(CGEventTapLocation.HID, drag_evt);
                CFRelease(drag_evt);
                Thread.Sleep(20);
            }

            // Mouse up at end
            var endPoint = new CGPoint(to_x, to_y);
            var up = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseUp, endPoint, CGMouseButton.Left);
            CGEventPost(CGEventTapLocation.HID, up);
            CFRelease(up);

            return ToolResult.Ok($"dragged from ({from_x},{from_y}) to ({to_x},{to_y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.Scroll)]
    public static string scroll(int clicks)
    {
        return ToolResult.Run("scroll", () =>
        {
            var scrollEvt = CGEventCreateScrollWheelEvent(IntPtr.Zero, CGScrollEventUnit.Line, 1, clicks);
            CGEventPost(CGEventTapLocation.HID, scrollEvt);
            CFRelease(scrollEvt);

            var direction = clicks > 0 ? "up" : "down";
            return ToolResult.Ok($"scrolled {direction} {Math.Abs(clicks)} click(s) at current mouse position");
        });
    }

    [McpServerTool, Description(ToolDescriptions.MoveMouse)]
    public static string move_mouse(int x, int y)
    {
        return ToolResult.Run("move_mouse", () =>
        {
            var point = new CGPoint(x, y);
            var evt = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.MouseMoved, point, CGMouseButton.Left);
            CGEventPost(CGEventTapLocation.HID, evt);
            CFRelease(evt);
            return ToolResult.Ok($"moved mouse to ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.TypeText)]
    public static string type_text(string text)
    {
        try
        {
            // Type each character using CGEvent with unicode string
            // This sends key events at the HID level, respecting the current input focus
            foreach (var ch in text)
            {
                var keyDown = CGEventCreateKeyboardEvent(IntPtr.Zero, 0, true);
                var keyUp = CGEventCreateKeyboardEvent(IntPtr.Zero, 0, false);

                // Set the unicode character on the event
                var chars = new ushort[] { ch };
                CGEventKeyboardSetUnicodeString(keyDown, 1, chars);
                CGEventKeyboardSetUnicodeString(keyUp, 1, chars);

                CGEventPost(CGEventTapLocation.HID, keyDown);
                Thread.Sleep(10);
                CGEventPost(CGEventTapLocation.HID, keyUp);
                Thread.Sleep(10);

                CFRelease(keyDown);
                CFRelease(keyUp);
            }

            return $"OK: typed '{text}' into focused window";

        }
        catch (Exception ex)
        {
            return $"ERROR: type_text failed: {ex.Message}";
        }
    }

    [McpServerTool, Description(ToolDescriptions.SendKey)]
    public static string send_key(string key)
    {
        try
        {
            ushort keyCode = key.ToLowerInvariant() switch
            {
                "escape" or "esc" => 53,
                "enter" or "return" => 36,
                "tab" => 48,
                "space" => 49,
                _ => ushort.MaxValue
            };

            if (keyCode == ushort.MaxValue)
                return $"ERROR: unsupported key '{key}'. Supported: escape, enter, tab, space";

            var down = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, true);
            var up = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, false);
            CGEventPost(CGEventTapLocation.HID, down);
            Thread.Sleep(50);
            CGEventPost(CGEventTapLocation.HID, up);
            CFRelease(down);
            CFRelease(up);

            return $"OK: sent '{key}' to focused window";

        }
        catch (Exception ex)
        {
            return $"ERROR: send_key failed: {ex.Message}";
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void ClickAtPoint(double x, double y)
    {
        var point = new CGPoint(x, y);
        var down = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseDown, point, CGMouseButton.Left);
        var up = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.LeftMouseUp, point, CGMouseButton.Left);
        CGEventPost(CGEventTapLocation.HID, down);
        Thread.Sleep(50);
        CGEventPost(CGEventTapLocation.HID, up);
        CFRelease(down);
        CFRelease(up);
    }

}

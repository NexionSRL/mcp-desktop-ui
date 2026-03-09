using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace MacOsMcp;

[McpServerToolType]
public static class UiTools
{
    public static string? ScreenshotDir { get; set; }

    // ── CoreGraphics P/Invoke ──────────────────────────────────────────────

    private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
    private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
    private const string ApplicationServices = "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices";

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

    // CGWindowList
    [DllImport(CoreGraphics)]
    private static extern IntPtr CGWindowListCopyWindowInfo(CGWindowListOption option, uint relativeToWindow);

    // CoreFoundation
    [DllImport(CoreFoundation)]
    private static extern long CFArrayGetCount(IntPtr theArray);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, long idx);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport(CoreFoundation)]
    private static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);

    [DllImport(CoreFoundation)]
    private static extern bool CFNumberGetValue(IntPtr number, int theType, out double value);

    [DllImport(CoreFoundation)]
    private static extern long CFStringGetLength(IntPtr theString);

    [DllImport(CoreFoundation)]
    private static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, long bufferSize, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

    [DllImport(CoreFoundation)]
    private static extern void CFRelease(IntPtr cf);

    [DllImport(CoreFoundation)]
    private static extern bool CFDictionaryGetValueIfPresent(IntPtr theDict, IntPtr key, out IntPtr value);

    // Accessibility
    [DllImport(ApplicationServices)]
    private static extern IntPtr AXUIElementCreateApplication(int pid);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementCopyAttributeValue(IntPtr element, IntPtr attribute, out IntPtr value);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementPerformAction(IntPtr element, IntPtr action);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementCopyAttributeValues(IntPtr element, IntPtr attribute, int index, int maxValues, out IntPtr values);

    [DllImport(ApplicationServices)]
    private static extern int AXUIElementGetTypeID();

    [DllImport(CoreFoundation)]
    private static extern int CFGetTypeID(IntPtr cf);

    [DllImport(CoreFoundation)]
    private static extern int CFBooleanGetValue(IntPtr boolean);

    private const uint kCFStringEncodingUTF8 = 0x08000100;
    private const int kCFNumberSInt32Type = 3;
    private const int kCFNumberFloat64Type = 13;
    private const int kAXErrorSuccess = 0;

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

    [Flags]
    private enum CGWindowListOption : uint
    {
        OptionOnScreenOnly = (1 << 0),
        ExcludeDesktopElements = (1 << 4),
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CGPoint
    {
        public double X;
        public double Y;
        public CGPoint(double x, double y) { X = x; Y = y; }
    }

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
                if (windowId == 0)
                    return $"ERROR: window '{window_title}' not found";
                args = $"-l {windowId} -o \"{tempPath}\"";
            }
            else
            {
                args = $"\"{tempPath}\"";
            }

            var psi = new ProcessStartInfo("screencapture", args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi)!;
            proc.WaitForExit(10000);

            if (proc.ExitCode != 0 || !File.Exists(tempPath))
                return $"ERROR: screencapture failed (exit code {proc.ExitCode})";

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
            var windows = GetWindowList();
            if (windows.Count == 0)
                return "No visible windows found";

            var sb = new StringBuilder();
            int i = 1;
            foreach (var (ownerName, windowName, _, _) in windows)
            {
                var title = string.IsNullOrEmpty(windowName) ? ownerName : $"{ownerName} - {windowName}";
                sb.AppendLine($"{i++}. {title}");
            }
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"ERROR: list_windows failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the accessibility element tree for a window. Returns element roles, titles, and enabled state. Has a 5-second timeout and max depth of 10 levels.")]
    public static string get_ui_tree(string window_title)
    {
        try
        {
            var (pid, _) = FindWindowPidAndId(window_title);
            if (pid == 0)
                return $"ERROR: window '{window_title}' not found";

            var appRef = AXUIElementCreateApplication(pid);
            if (appRef == IntPtr.Zero)
                return $"ERROR: could not create accessibility element for '{window_title}'";

            try
            {
                // Find the window element
                var windowEl = FindAXWindow(appRef, window_title);
                if (windowEl == IntPtr.Zero)
                    windowEl = appRef;

                var sb = new StringBuilder();
                var task = Task.Run(() => WalkAXTree(windowEl, sb, 0));
                if (!task.Wait(TimeSpan.FromSeconds(5)))
                    return sb.Length == 0
                        ? $"ERROR: timed out reading UI tree for '{window_title}'"
                        : $"(partial, timed out)\n{sb}";

                return sb.Length == 0 ? "(empty tree)" : sb.ToString();
            }
            finally
            {
                CFRelease(appRef);
            }
        }
        catch (Exception ex)
        {
            return $"ERROR: get_ui_tree failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Click a UI element by name inside a window. Uses macOS Accessibility API to find and press the element, with fallback to clicking at element center.")]
    public static string click_element(string window_title, string element_name)
    {
        try
        {
            var (pid, _) = FindWindowPidAndId(window_title);
            if (pid == 0)
                return $"ERROR: window '{window_title}' not found";

            var appRef = AXUIElementCreateApplication(pid);
            if (appRef == IntPtr.Zero)
                return $"ERROR: could not create accessibility element for '{window_title}'";

            try
            {
                var windowEl = FindAXWindow(appRef, window_title);
                if (windowEl == IntPtr.Zero)
                    windowEl = appRef;

                var element = FindAXElementByName(windowEl, element_name, 0);
                if (element == IntPtr.Zero)
                    return $"ERROR: element '{element_name}' not found in '{window_title}'";

                // Try AXPress action first
                var pressAction = CFStringCreateWithCString(IntPtr.Zero, "AXPress", kCFStringEncodingUTF8);
                try
                {
                    var result = AXUIElementPerformAction(element, pressAction);
                    if (result == kAXErrorSuccess)
                        return $"OK: pressed '{element_name}'";
                }
                finally
                {
                    CFRelease(pressAction);
                }

                // Fallback: click at element center
                var (cx, cy) = GetAXElementCenter(element);
                if (cx >= 0 && cy >= 0)
                {
                    ClickAtPoint(cx, cy);
                    return $"OK: clicked '{element_name}' at ({cx},{cy})";
                }

                return $"ERROR: could not click '{element_name}' - no position available";
            }
            finally
            {
                CFRelease(appRef);
            }
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
            var point = new CGPoint(x, y);
            var down = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.RightMouseDown, point, CGMouseButton.Right);
            var up = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.RightMouseUp, point, CGMouseButton.Right);
            CGEventPost(CGEventTapLocation.HID, down);
            Thread.Sleep(50);
            CGEventPost(CGEventTapLocation.HID, up);
            CFRelease(down);
            CFRelease(up);
            return $"OK: right-clicked at ({x},{y})";
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
            return $"OK: double-clicked at ({x},{y})";
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

            return $"OK: dragged from ({from_x},{from_y}) to ({to_x},{to_y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: drag failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Scroll up or down inside a window. Positive clicks = scroll up, negative = scroll down. Focuses the window first and moves mouse to window center.")]
    public static string scroll(string window_title, int clicks)
    {
        try
        {
            var (ownerName, rect) = FindWindowOwnerAndRect(window_title);
            if (ownerName == null)
                return $"ERROR: window '{window_title}' not found";

            // Focus window via AppleScript
            RunAppleScript($"tell application \"{EscapeAppleScript(ownerName)}\" to activate");
            Thread.Sleep(200);

            // Move mouse to window center
            if (rect.HasValue)
            {
                var (rx, ry, rw, rh) = rect.Value;
                var center = new CGPoint(rx + rw / 2.0, ry + rh / 2.0);
                var moveEvt = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.MouseMoved, center, CGMouseButton.Left);
                CGEventPost(CGEventTapLocation.HID, moveEvt);
                CFRelease(moveEvt);
                Thread.Sleep(50);
            }

            var scrollEvt = CGEventCreateScrollWheelEvent(IntPtr.Zero, CGScrollEventUnit.Line, 1, clicks);
            CGEventPost(CGEventTapLocation.HID, scrollEvt);
            CFRelease(scrollEvt);

            var direction = clicks > 0 ? "up" : "down";
            return $"OK: scrolled {direction} {Math.Abs(clicks)} click(s) in '{window_title}'";
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
            // Move mouse to position
            var point = new CGPoint(x, y);
            var moveEvt = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.MouseMoved, point, CGMouseButton.Left);
            CGEventPost(CGEventTapLocation.HID, moveEvt);
            CFRelease(moveEvt);
            Thread.Sleep(50);

            var scrollEvt = CGEventCreateScrollWheelEvent(IntPtr.Zero, CGScrollEventUnit.Line, 1, clicks);
            CGEventPost(CGEventTapLocation.HID, scrollEvt);
            CFRelease(scrollEvt);

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
            var point = new CGPoint(x, y);
            var evt = CGEventCreateMouseEvent(IntPtr.Zero, CGEventType.MouseMoved, point, CGMouseButton.Left);
            CGEventPost(CGEventTapLocation.HID, evt);
            CFRelease(evt);
            return $"OK: moved mouse to ({x},{y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: move_mouse failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Type text into a window using AppleScript keystroke. Use click_at to focus a text field first.")]
    public static string type_text(string window_title, string text)
    {
        try
        {
            var (ownerName, _) = FindWindowOwnerAndRect(window_title);
            if (ownerName == null)
                return $"ERROR: window '{window_title}' not found";

            // Focus window first
            RunAppleScript($"tell application \"{EscapeAppleScript(ownerName)}\" to activate");
            Thread.Sleep(200);

            RunAppleScript($"tell application \"System Events\" to keystroke \"{EscapeAppleScript(text)}\"");
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
            var (ownerName, _) = FindWindowOwnerAndRect(window_title);
            if (ownerName == null)
                return $"ERROR: window '{window_title}' not found";

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

            // Focus window
            RunAppleScript($"tell application \"{EscapeAppleScript(ownerName)}\" to activate");
            Thread.Sleep(200);

            var down = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, true);
            var up = CGEventCreateKeyboardEvent(IntPtr.Zero, keyCode, false);
            CGEventPost(CGEventTapLocation.HID, down);
            Thread.Sleep(50);
            CGEventPost(CGEventTapLocation.HID, up);
            CFRelease(down);
            CFRelease(up);

            return $"OK: sent '{key}' to '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: send_key failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Bring a window to the foreground using AppleScript.")]
    public static string focus_window(string window_title)
    {
        try
        {
            var (ownerName, _) = FindWindowOwnerAndRect(window_title);
            if (ownerName == null)
                return $"ERROR: window '{window_title}' not found";

            RunAppleScript($"tell application \"{EscapeAppleScript(ownerName)}\" to activate");
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
            var (ownerName, rect) = FindWindowOwnerAndRect(window_title);
            if (ownerName == null)
                return $"ERROR: window '{window_title}' not found";

            if (!rect.HasValue)
                return $"ERROR: could not get window rect for '{window_title}'";

            var (x, y, w, h) = rect.Value;
            return $"x={x} y={y} width={w} height={h}";
        }
        catch (Exception ex)
        {
            return $"ERROR: get_window_rect failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Close a window using Accessibility API AXCloseButton, with fallback to AppleScript.")]
    public static string close_window(string window_title)
    {
        try
        {
            var (pid, _) = FindWindowPidAndId(window_title);
            if (pid == 0)
                return $"ERROR: window '{window_title}' not found";

            var appRef = AXUIElementCreateApplication(pid);
            if (appRef == IntPtr.Zero)
                return $"ERROR: could not create accessibility element for '{window_title}'";

            try
            {
                var windowEl = FindAXWindow(appRef, window_title);
                if (windowEl != IntPtr.Zero)
                {
                    // Try AXCloseButton
                    var closeButtonAttr = CFStringCreateWithCString(IntPtr.Zero, "AXCloseButton", kCFStringEncodingUTF8);
                    try
                    {
                        if (AXUIElementCopyAttributeValue(windowEl, closeButtonAttr, out var closeButton) == kAXErrorSuccess
                            && closeButton != IntPtr.Zero)
                        {
                            var pressAction = CFStringCreateWithCString(IntPtr.Zero, "AXPress", kCFStringEncodingUTF8);
                            try
                            {
                                if (AXUIElementPerformAction(closeButton, pressAction) == kAXErrorSuccess)
                                    return $"OK: closed '{window_title}'";
                            }
                            finally
                            {
                                CFRelease(pressAction);
                            }
                        }
                    }
                    finally
                    {
                        CFRelease(closeButtonAttr);
                    }
                }
            }
            finally
            {
                CFRelease(appRef);
            }

            // Fallback: AppleScript
            var (ownerName, _) = FindWindowOwnerAndRect(window_title);
            if (ownerName != null)
            {
                RunAppleScript($"tell application \"{EscapeAppleScript(ownerName)}\" to close front window");
                return $"OK: closed '{window_title}' via AppleScript";
            }

            return $"ERROR: could not close '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: close_window failed: {ex.Message}";
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

    private static string GetCFString(IntPtr cfString)
    {
        if (cfString == IntPtr.Zero) return "";
        var length = CFStringGetLength(cfString);
        if (length == 0) return "";
        var buffer = new byte[(length + 1) * 4];
        return CFStringGetCString(cfString, buffer, buffer.Length, kCFStringEncodingUTF8)
            ? System.Text.Encoding.UTF8.GetString(buffer, 0, Array.IndexOf(buffer, (byte)0))
            : "";
    }

    private static IntPtr CreateCFString(string s) =>
        CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8);

    /// <summary>Returns list of (ownerName, windowName, windowId, pid)</summary>
    private static List<(string ownerName, string windowName, uint windowId, int pid)> GetWindowList()
    {
        var result = new List<(string, string, uint, int)>();
        var windowList = CGWindowListCopyWindowInfo(
            CGWindowListOption.OptionOnScreenOnly | CGWindowListOption.ExcludeDesktopElements, 0);
        if (windowList == IntPtr.Zero) return result;

        try
        {
            var kOwnerName = CreateCFString("kCGWindowOwnerName");
            var kName = CreateCFString("kCGWindowName");
            var kNumber = CreateCFString("kCGWindowNumber");
            var kOwnerPID = CreateCFString("kCGWindowOwnerPID");
            var kLayer = CreateCFString("kCGWindowLayer");

            try
            {
                var count = CFArrayGetCount(windowList);
                for (long i = 0; i < count; i++)
                {
                    var dict = CFArrayGetValueAtIndex(windowList, i);
                    if (dict == IntPtr.Zero) continue;

                    // Skip non-layer-0 windows (menus, etc.)
                    var layerPtr = CFDictionaryGetValue(dict, kLayer);
                    if (layerPtr != IntPtr.Zero)
                    {
                        CFNumberGetValue(layerPtr, kCFNumberSInt32Type, out int layer);
                        if (layer != 0) continue;
                    }

                    var ownerName = GetCFString(CFDictionaryGetValue(dict, kOwnerName));
                    var windowName = GetCFString(CFDictionaryGetValue(dict, kName));

                    if (string.IsNullOrEmpty(ownerName)) continue;

                    var numberPtr = CFDictionaryGetValue(dict, kNumber);
                    uint windowId = 0;
                    if (numberPtr != IntPtr.Zero)
                    {
                        CFNumberGetValue(numberPtr, kCFNumberSInt32Type, out int id);
                        windowId = (uint)id;
                    }

                    int pid = 0;
                    var pidPtr = CFDictionaryGetValue(dict, kOwnerPID);
                    if (pidPtr != IntPtr.Zero)
                        CFNumberGetValue(pidPtr, kCFNumberSInt32Type, out pid);

                    result.Add((ownerName, windowName, windowId, pid));
                }
            }
            finally
            {
                CFRelease(kOwnerName);
                CFRelease(kName);
                CFRelease(kNumber);
                CFRelease(kOwnerPID);
                CFRelease(kLayer);
            }
        }
        finally
        {
            CFRelease(windowList);
        }

        return result;
    }

    private static uint FindWindowId(string titleFragment)
    {
        foreach (var (ownerName, windowName, windowId, _) in GetWindowList())
        {
            if (ownerName.Contains(titleFragment, StringComparison.OrdinalIgnoreCase) ||
                windowName.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
                return windowId;
        }
        return 0;
    }

    private static (int pid, uint windowId) FindWindowPidAndId(string titleFragment)
    {
        foreach (var (ownerName, windowName, windowId, pid) in GetWindowList())
        {
            if (ownerName.Contains(titleFragment, StringComparison.OrdinalIgnoreCase) ||
                windowName.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
                return (pid, windowId);
        }
        return (0, 0);
    }

    private static (string? ownerName, (double x, double y, double w, double h)?) FindWindowOwnerAndRect(string titleFragment)
    {
        var windowList = CGWindowListCopyWindowInfo(
            CGWindowListOption.OptionOnScreenOnly | CGWindowListOption.ExcludeDesktopElements, 0);
        if (windowList == IntPtr.Zero) return (null, null);

        try
        {
            var kOwnerName = CreateCFString("kCGWindowOwnerName");
            var kName = CreateCFString("kCGWindowName");
            var kBounds = CreateCFString("kCGWindowBounds");
            var kLayer = CreateCFString("kCGWindowLayer");

            try
            {
                var count = CFArrayGetCount(windowList);
                for (long i = 0; i < count; i++)
                {
                    var dict = CFArrayGetValueAtIndex(windowList, i);
                    if (dict == IntPtr.Zero) continue;

                    var layerPtr = CFDictionaryGetValue(dict, kLayer);
                    if (layerPtr != IntPtr.Zero)
                    {
                        CFNumberGetValue(layerPtr, kCFNumberSInt32Type, out int layer);
                        if (layer != 0) continue;
                    }

                    var ownerName = GetCFString(CFDictionaryGetValue(dict, kOwnerName));
                    var windowName = GetCFString(CFDictionaryGetValue(dict, kName));

                    if (!ownerName.Contains(titleFragment, StringComparison.OrdinalIgnoreCase) &&
                        !windowName.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Parse bounds dict
                    var boundsDict = CFDictionaryGetValue(dict, kBounds);
                    if (boundsDict != IntPtr.Zero)
                    {
                        var kX = CreateCFString("X");
                        var kY = CreateCFString("Y");
                        var kW = CreateCFString("Width");
                        var kH = CreateCFString("Height");
                        try
                        {
                            double x = 0, y = 0, w = 0, h = 0;
                            var xPtr = CFDictionaryGetValue(boundsDict, kX);
                            var yPtr = CFDictionaryGetValue(boundsDict, kY);
                            var wPtr = CFDictionaryGetValue(boundsDict, kW);
                            var hPtr = CFDictionaryGetValue(boundsDict, kH);
                            if (xPtr != IntPtr.Zero) CFNumberGetValue(xPtr, kCFNumberFloat64Type, out x);
                            if (yPtr != IntPtr.Zero) CFNumberGetValue(yPtr, kCFNumberFloat64Type, out y);
                            if (wPtr != IntPtr.Zero) CFNumberGetValue(wPtr, kCFNumberFloat64Type, out w);
                            if (hPtr != IntPtr.Zero) CFNumberGetValue(hPtr, kCFNumberFloat64Type, out h);
                            return (ownerName, (x, y, w, h));
                        }
                        finally
                        {
                            CFRelease(kX);
                            CFRelease(kY);
                            CFRelease(kW);
                            CFRelease(kH);
                        }
                    }

                    return (ownerName, null);
                }
            }
            finally
            {
                CFRelease(kOwnerName);
                CFRelease(kName);
                CFRelease(kBounds);
                CFRelease(kLayer);
            }
        }
        finally
        {
            CFRelease(windowList);
        }

        return (null, null);
    }

    private static IntPtr FindAXWindow(IntPtr appRef, string titleFragment)
    {
        var windowsAttr = CreateCFString("AXWindows");
        try
        {
            if (AXUIElementCopyAttributeValue(appRef, windowsAttr, out var windowsRef) != kAXErrorSuccess
                || windowsRef == IntPtr.Zero)
                return IntPtr.Zero;

            var titleAttr = CreateCFString("AXTitle");
            try
            {
                var count = CFArrayGetCount(windowsRef);
                for (long i = 0; i < count; i++)
                {
                    var win = CFArrayGetValueAtIndex(windowsRef, i);
                    if (win == IntPtr.Zero) continue;

                    if (AXUIElementCopyAttributeValue(win, titleAttr, out var titleRef) == kAXErrorSuccess
                        && titleRef != IntPtr.Zero)
                    {
                        var title = GetCFString(titleRef);
                        if (title.Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
                            return win;
                    }
                }
            }
            finally
            {
                CFRelease(titleAttr);
            }

            // Return first window as fallback
            if (CFArrayGetCount(windowsRef) > 0)
                return CFArrayGetValueAtIndex(windowsRef, 0);
        }
        finally
        {
            CFRelease(windowsAttr);
        }

        return IntPtr.Zero;
    }

    private static IntPtr FindAXElementByName(IntPtr element, string name, int depth)
    {
        if (depth > 10) return IntPtr.Zero;

        // Check this element's title/description
        var titleAttr = CreateCFString("AXTitle");
        var descAttr = CreateCFString("AXDescription");
        try
        {
            if (AXUIElementCopyAttributeValue(element, titleAttr, out var titleRef) == kAXErrorSuccess
                && titleRef != IntPtr.Zero)
            {
                var title = GetCFString(titleRef);
                if (title.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return element;
            }

            if (AXUIElementCopyAttributeValue(element, descAttr, out var descRef) == kAXErrorSuccess
                && descRef != IntPtr.Zero)
            {
                var desc = GetCFString(descRef);
                if (desc.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return element;
            }
        }
        finally
        {
            CFRelease(titleAttr);
            CFRelease(descAttr);
        }

        // Search children
        var childrenAttr = CreateCFString("AXChildren");
        try
        {
            if (AXUIElementCopyAttributeValue(element, childrenAttr, out var childrenRef) == kAXErrorSuccess
                && childrenRef != IntPtr.Zero)
            {
                var count = CFArrayGetCount(childrenRef);
                for (long i = 0; i < count; i++)
                {
                    var child = CFArrayGetValueAtIndex(childrenRef, i);
                    if (child == IntPtr.Zero) continue;
                    var found = FindAXElementByName(child, name, depth + 1);
                    if (found != IntPtr.Zero) return found;
                }
            }
        }
        finally
        {
            CFRelease(childrenAttr);
        }

        return IntPtr.Zero;
    }

    private static (double x, double y) GetAXElementCenter(IntPtr element)
    {
        var posAttr = CreateCFString("AXPosition");
        var sizeAttr = CreateCFString("AXSize");
        try
        {
            if (AXUIElementCopyAttributeValue(element, posAttr, out var posRef) == kAXErrorSuccess
                && posRef != IntPtr.Zero
                && AXUIElementCopyAttributeValue(element, sizeAttr, out var sizeRef) == kAXErrorSuccess
                && sizeRef != IntPtr.Zero)
            {
                // AXPosition and AXSize are AXValue types - extract via CoreFoundation
                // They contain CGPoint and CGSize as AXValueRef
                // Use AXValueGetValue P/Invoke
                if (AXValueGetValue(posRef, 1, out var pos) && AXValueGetSize(sizeRef, 2, out var size))
                {
                    return (pos.X + size.X / 2.0, pos.Y + size.Y / 2.0);
                }
            }
        }
        finally
        {
            CFRelease(posAttr);
            CFRelease(sizeAttr);
        }
        return (-1, -1);
    }

    [DllImport(ApplicationServices)]
    private static extern bool AXValueGetValue(IntPtr value, int type, out CGPoint point);

    [DllImport(ApplicationServices, EntryPoint = "AXValueGetValue")]
    private static extern bool AXValueGetSize(IntPtr value, int type, out CGSize size);

    [StructLayout(LayoutKind.Sequential)]
    private struct CGSize
    {
        public double X; // width
        public double Y; // height
    }

    private static void WalkAXTree(IntPtr element, StringBuilder sb, int depth)
    {
        if (depth > 10) return;

        var indent = new string(' ', depth * 2);
        var roleAttr = CreateCFString("AXRole");
        var titleAttr = CreateCFString("AXTitle");
        var descAttr = CreateCFString("AXDescription");
        var valueAttr = CreateCFString("AXValue");
        var enabledAttr = CreateCFString("AXEnabled");

        try
        {
            var role = "";
            var title = "";
            var desc = "";
            var value = "";
            var enabled = true;

            if (AXUIElementCopyAttributeValue(element, roleAttr, out var roleRef) == kAXErrorSuccess && roleRef != IntPtr.Zero)
                role = GetCFString(roleRef);
            if (AXUIElementCopyAttributeValue(element, titleAttr, out var titleRef) == kAXErrorSuccess && titleRef != IntPtr.Zero)
                title = GetCFString(titleRef);
            if (AXUIElementCopyAttributeValue(element, descAttr, out var descRef) == kAXErrorSuccess && descRef != IntPtr.Zero)
                desc = GetCFString(descRef);
            if (AXUIElementCopyAttributeValue(element, valueAttr, out var valueRef) == kAXErrorSuccess && valueRef != IntPtr.Zero)
            {
                // Value could be any type; try to get as string
                if (CFGetTypeID(valueRef) == CFStringGetTypeID())
                    value = GetCFString(valueRef);
            }
            if (AXUIElementCopyAttributeValue(element, enabledAttr, out var enabledRef) == kAXErrorSuccess && enabledRef != IntPtr.Zero)
                enabled = CFBooleanGetValue(enabledRef) != 0;

            var parts = new List<string>();
            if (!string.IsNullOrEmpty(title)) parts.Add($"title=\"{title}\"");
            if (!string.IsNullOrEmpty(desc)) parts.Add($"desc=\"{desc}\"");
            if (!string.IsNullOrEmpty(value)) parts.Add($"value=\"{value}\"");
            parts.Add($"enabled={enabled}");

            sb.AppendLine($"{indent}[{role}] {string.Join(" ", parts)}");
        }
        finally
        {
            CFRelease(roleAttr);
            CFRelease(titleAttr);
            CFRelease(descAttr);
            CFRelease(valueAttr);
            CFRelease(enabledAttr);
        }

        // Walk children
        var childrenAttr = CreateCFString("AXChildren");
        try
        {
            if (AXUIElementCopyAttributeValue(element, childrenAttr, out var childrenRef) == kAXErrorSuccess
                && childrenRef != IntPtr.Zero)
            {
                var count = CFArrayGetCount(childrenRef);
                for (long i = 0; i < count; i++)
                {
                    var child = CFArrayGetValueAtIndex(childrenRef, i);
                    if (child != IntPtr.Zero)
                        WalkAXTree(child, sb, depth + 1);
                }
            }
        }
        finally
        {
            CFRelease(childrenAttr);
        }
    }

    [DllImport(CoreFoundation)]
    private static extern int CFStringGetTypeID();

    private static string RunAppleScript(string script)
    {
        var psi = new ProcessStartInfo("osascript", $"-e '{script}'")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi)!;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return output.Trim();
    }

    private static string EscapeAppleScript(string s) =>
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

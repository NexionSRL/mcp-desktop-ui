using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WindowsMcp;

[McpServerToolType]
public static class UiTools
{
    public static string? ScreenshotDir { get; set; }

    // ── Win32 P/Invoke ─────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    private const uint WM_CLOSE = 0x0010;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_MOUSEWHEEL = 0x020A;
    private const int VK_TAB = 0x09;
    private const int VK_SPACE = 0x20;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_RETURN = 0x0D;
    private const int SW_RESTORE = 9;
    private const int WHEEL_DELTA = 120;

    private const int INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public MOUSEINPUT mi;
    }

    // ── Tools ──────────────────────────────────────────────────────────────

    [McpServerTool, Description("Capture a screenshot. If window_title is given, captures only that window; otherwise captures the full primary screen. Returns PNG as base64, or saves to configured screenshot directory and returns file path.")]
    public static string screenshot(string? window_title = null)
    {
        try
        {
            Rectangle bounds;
            if (window_title != null)
            {
                var hwnd = FindWindow(window_title);
                if (hwnd == IntPtr.Zero)
                    return $"ERROR: window '{window_title}' not found";
                if (!GetWindowRect(hwnd, out var r))
                    return "ERROR: could not get window rect";
                bounds = new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
            }
            else
            {
                bounds = new Rectangle(0, 0,
                    System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width,
                    System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height);
            }

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return "ERROR: invalid window bounds";

            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            if (!string.IsNullOrEmpty(ScreenshotDir))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var label = string.IsNullOrEmpty(window_title) ? "screen" : SanitizeFileName(window_title);
                var fileName = $"{timestamp}_{label}.png";
                var filePath = Path.Combine(ScreenshotDir, fileName);
                bmp.Save(filePath, ImageFormat.Png);
                return $"OK: saved screenshot to {filePath}";
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
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
            var titles = new List<string>();
            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd)) return true;
                var sb = new StringBuilder(256);
                if (GetWindowText(hWnd, sb, 256) > 0)
                    titles.Add(sb.ToString());
                return true;
            }, IntPtr.Zero);

            return titles.Count == 0
                ? "No visible windows found"
                : string.Join("\n", titles.Select((t, i) => $"{i + 1}. {t}"));
        }
        catch (Exception ex)
        {
            return $"ERROR: list_windows failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the UI automation element tree for a window. Returns element names, types, and enabled state. Has a 5-second timeout and max depth of 10 levels.")]
    public static string get_ui_tree(string window_title)
    {
        try
        {
            var root = FindAutomationElement(window_title);
            if (root == null)
                return $"ERROR: window '{window_title}' not found";

            var sb = new StringBuilder();
            var task = Task.Run(() => WalkTree(root, sb, 0));
            if (!task.Wait(TimeSpan.FromSeconds(5)))
                return sb.Length == 0
                    ? $"ERROR: timed out reading UI tree for '{window_title}'"
                    : $"(partial, timed out)\n{sb}";

            return sb.Length == 0 ? "(empty tree)" : sb.ToString();
        }
        catch (Exception ex)
        {
            return $"ERROR: get_ui_tree failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Click a UI element by name inside a window. Uses Windows UI Automation to find and invoke the element, with fallback to mouse click at element center.")]
    public static string click_element(string window_title, string element_name)
    {
        try
        {
            var root = FindAutomationElement(window_title);
            if (root == null)
                return $"ERROR: window '{window_title}' not found";

            var condition = new PropertyCondition(AutomationElement.NameProperty, element_name);
            var element = root.FindFirst(TreeScope.Descendants, condition);
            if (element == null)
                return $"ERROR: element '{element_name}' not found in '{window_title}'";

            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
            {
                try
                {
                    var task = Task.Run(() => ((InvokePattern)pattern).Invoke());
                    if (task.Wait(TimeSpan.FromSeconds(3)))
                        return $"OK: invoked '{element_name}'";
                    else
                        return $"OK: invoked '{element_name}' (timed out waiting for response, likely dialog closed)";
                }
                catch
                {
                    return $"OK: invoked '{element_name}' (exception, likely dialog closed)";
                }
            }

            return ClickAtElementCenter(element, element_name);
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
            MoveMouse(x, y);
            var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } };
            var up = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } };
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(50);
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());
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
            MoveMouse(x, y);
            var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTDOWN } };
            var up = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTUP } };
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(50);
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());
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
            MoveMouse(x, y);
            for (int i = 0; i < 2; i++)
            {
                var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } };
                var up = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } };
                SendInput(1, ref down, Marshal.SizeOf<INPUT>());
                Thread.Sleep(30);
                SendInput(1, ref up, Marshal.SizeOf<INPUT>());
                if (i == 0) Thread.Sleep(50);
            }
            return $"OK: double-clicked at ({x},{y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: double_click_at failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Drag from one screen coordinate to another. Holds left mouse button at (from_x, from_y), moves to (to_x, to_y), then releases.")]
    public static string drag(int from_x, int from_y, int to_x, int to_y)
    {
        try
        {
            MoveMouse(from_x, from_y);
            Thread.Sleep(50);

            var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } };
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(100);

            MoveMouse(to_x, to_y);
            Thread.Sleep(100);

            var up = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } };
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());

            return $"OK: dragged from ({from_x},{from_y}) to ({to_x},{to_y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: drag failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Scroll up or down inside a window. Positive clicks = scroll up, negative = scroll down. Each click is one notch of the mouse wheel.")]
    public static string scroll(string window_title, int clicks)
    {
        try
        {
            var hwnd = FindWindow(window_title);
            if (hwnd == IntPtr.Zero)
                return $"ERROR: window '{window_title}' not found";

            SetForegroundWindow(hwnd);
            Thread.Sleep(100);

            int delta = clicks * WHEEL_DELTA;
            IntPtr wParam = (IntPtr)(delta << 16);
            if (GetWindowRect(hwnd, out var r))
            {
                int cx = (r.Left + r.Right) / 2;
                int cy = (r.Top + r.Bottom) / 2;
                IntPtr lParam = (IntPtr)((cy << 16) | (cx & 0xFFFF));
                PostMessage(hwnd, WM_MOUSEWHEEL, wParam, lParam);
            }
            else
            {
                PostMessage(hwnd, WM_MOUSEWHEEL, wParam, IntPtr.Zero);
            }

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
            MoveMouse(x, y);
            Thread.Sleep(50);

            var wheel = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT
                {
                    mouseData = (uint)(clicks * WHEEL_DELTA),
                    dwFlags = MOUSEEVENTF_WHEEL
                }
            };
            SendInput(1, ref wheel, Marshal.SizeOf<INPUT>());

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
            MoveMouse(x, y);
            return $"OK: moved mouse to ({x},{y})";
        }
        catch (Exception ex)
        {
            return $"ERROR: move_mouse failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Type text into the currently focused control of a window. Use click_at to focus a text field first.")]
    public static string type_text(string window_title, string text)
    {
        try
        {
            var hwnd = FindWindow(window_title);
            if (hwnd == IntPtr.Zero)
                return $"ERROR: window '{window_title}' not found";

            SetForegroundWindow(hwnd);
            Thread.Sleep(200);

            var result = "";
            var t = new Thread(() =>
            {
                try
                {
                    System.Windows.Forms.SendKeys.SendWait(text);
                    result = $"OK: typed '{text}' into '{window_title}'";
                }
                catch (Exception ex)
                {
                    result = $"ERROR: SendKeys failed: {ex.Message}";
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join(TimeSpan.FromSeconds(5));
            return string.IsNullOrEmpty(result) ? "ERROR: type_text timed out" : result;
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
            var hwnd = FindWindow(window_title);
            if (hwnd == IntPtr.Zero)
                return $"ERROR: window '{window_title}' not found";

            int vk = key.ToLowerInvariant() switch
            {
                "escape" or "esc" => VK_ESCAPE,
                "enter" or "return" => VK_RETURN,
                "tab" => VK_TAB,
                "space" => VK_SPACE,
                _ => -1
            };

            if (vk == -1)
                return $"ERROR: unsupported key '{key}'. Supported: escape, enter, tab, space";

            SetForegroundWindow(hwnd);
            Thread.Sleep(100);
            PostMessage(hwnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
            Thread.Sleep(50);
            PostMessage(hwnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
            return $"OK: sent '{key}' to '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: send_key failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Bring a window to the foreground and restore it if minimized.")]
    public static string focus_window(string window_title)
    {
        try
        {
            var hwnd = FindWindow(window_title);
            if (hwnd == IntPtr.Zero)
                return $"ERROR: window '{window_title}' not found";

            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
            return $"OK: focused '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: focus_window failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get the screen coordinates (left, top, right, bottom, width, height) of a window.")]
    public static string get_window_rect(string window_title)
    {
        try
        {
            var hwnd = FindWindow(window_title);
            if (hwnd == IntPtr.Zero)
                return $"ERROR: window '{window_title}' not found";

            if (!GetWindowRect(hwnd, out var r))
                return "ERROR: could not get window rect";

            return $"left={r.Left} top={r.Top} right={r.Right} bottom={r.Bottom} width={r.Right - r.Left} height={r.Bottom - r.Top}";
        }
        catch (Exception ex)
        {
            return $"ERROR: get_window_rect failed: {ex.Message}";
        }
    }

    [McpServerTool, Description("Close a window by sending WM_CLOSE.")]
    public static string close_window(string window_title)
    {
        try
        {
            var hwnd = FindWindow(window_title);
            if (hwnd == IntPtr.Zero)
                return $"ERROR: window '{window_title}' not found";

            PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return $"OK: sent WM_CLOSE to '{window_title}'";
        }
        catch (Exception ex)
        {
            return $"ERROR: close_window failed: {ex.Message}";
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void MoveMouse(int x, int y)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            mi = new MOUSEINPUT
            {
                dx = x * 65536 / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width,
                dy = y * 65536 / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height,
                dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
            }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static string ClickAtElementCenter(AutomationElement element, string element_name)
    {
        var rect = element.Current.BoundingRectangle;
        if (rect.IsEmpty)
            return $"ERROR: element '{element_name}' has no bounding rect";

        var cx = (int)(rect.Left + rect.Width / 2);
        var cy = (int)(rect.Top + rect.Height / 2);
        MoveMouse(cx, cy);

        var down = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTDOWN } };
        var up = new INPUT { type = INPUT_MOUSE, mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_LEFTUP } };
        SendInput(1, ref down, Marshal.SizeOf<INPUT>());
        Thread.Sleep(50);
        SendInput(1, ref up, Marshal.SizeOf<INPUT>());

        return $"OK: clicked '{element_name}' at ({cx},{cy})";
    }

    private static IntPtr FindWindow(string titleFragment)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            if (sb.ToString().Contains(titleFragment, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static AutomationElement? FindAutomationElement(string titleFragment)
    {
        var task = Task.Run(() =>
        {
            var condition = new PropertyCondition(AutomationElement.NameProperty, titleFragment,
                PropertyConditionFlags.IgnoreCase);
            var desktop = AutomationElement.RootElement;
            var el = desktop.FindFirst(TreeScope.Children, condition);
            if (el != null) return el;

            var hwnd = FindWindow(titleFragment);
            if (hwnd == IntPtr.Zero) return null;
            return AutomationElement.FromHandle(hwnd);
        });

        if (task.Wait(TimeSpan.FromSeconds(5)))
            return task.Result;

        throw new TimeoutException($"Timed out finding automation element for '{titleFragment}'.");
    }

    private static void WalkTree(AutomationElement el, StringBuilder sb, int depth)
    {
        if (depth > 10) return;

        var indent = new string(' ', depth * 2);
        var name = el.Current.Name;
        var type = el.Current.ControlType.ProgrammaticName.Replace("ControlType.", "");
        var enabled = el.Current.IsEnabled;

        if (!string.IsNullOrWhiteSpace(name) || depth == 0)
            sb.AppendLine($"{indent}[{type}] \"{name}\" enabled={enabled}");

        var walker = TreeWalker.ControlViewWalker;
        var child = walker.GetFirstChild(el);
        while (child != null)
        {
            WalkTree(child, sb, depth + 1);
            child = walker.GetNextSibling(child);
        }
    }

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

using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using McpDesktopUi.Common;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.ComponentModel;

namespace WindowsMcp;

[McpServerToolType]
public static class UiTools
{
    // ── Win32 P/Invoke ─────────────────────────────────────────────────────

    [DllImport("user32.dll")]
    private static extern uint SendInput(uint nInputs, ref INPUT pInputs, int cbSize);

    private const int VK_TAB = 0x09;
    private const int VK_SPACE = 0x20;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_RETURN = 0x0D;
    private const int WHEEL_DELTA = 120;

    private const int INPUT_MOUSE = 0;
    private const int INPUT_KEYBOARD = 1;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    private const uint KEYEVENTF_KEYUP = 0x0002;

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
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public INPUTUNION u;
    }

    // ── Tools ──────────────────────────────────────────────────────────────

    [McpServerTool, Description(ToolDescriptions.Screenshot)]
    public static CallToolResult screenshot()
    {
        try
        {
            var bounds = new Rectangle(0, 0,
                System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width,
                System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height);

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return new CallToolResult { IsError = true, Content = [new TextContentBlock { Text = "ERROR: invalid screen bounds" }] };

            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Jpeg);
            return ScreenshotHelper.ToImageResult(ms.ToArray(), "image/jpeg", null);
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
            MoveMouse(x, y);
            var down = CreateMouseInput(MOUSEEVENTF_LEFTDOWN);
            var up = CreateMouseInput(MOUSEEVENTF_LEFTUP);
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(50);
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());
            return ToolResult.Ok($"clicked at ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.RightClickAt)]
    public static string right_click_at(int x, int y)
    {
        return ToolResult.Run("right_click_at", () =>
        {
            MoveMouse(x, y);
            var down = CreateMouseInput(MOUSEEVENTF_RIGHTDOWN);
            var up = CreateMouseInput(MOUSEEVENTF_RIGHTUP);
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(50);
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());
            return ToolResult.Ok($"right-clicked at ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.DoubleClickAt)]
    public static string double_click_at(int x, int y)
    {
        return ToolResult.Run("double_click_at", () =>
        {
            MoveMouse(x, y);
            for (int i = 0; i < 2; i++)
            {
                var down = CreateMouseInput(MOUSEEVENTF_LEFTDOWN);
                var up = CreateMouseInput(MOUSEEVENTF_LEFTUP);
                SendInput(1, ref down, Marshal.SizeOf<INPUT>());
                Thread.Sleep(30);
                SendInput(1, ref up, Marshal.SizeOf<INPUT>());
                if (i == 0) Thread.Sleep(50);
            }
            return ToolResult.Ok($"double-clicked at ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.Drag)]
    public static string drag(int from_x, int from_y, int to_x, int to_y)
    {
        return ToolResult.Run("drag", () =>
        {
            MoveMouse(from_x, from_y);
            Thread.Sleep(50);

            var down = CreateMouseInput(MOUSEEVENTF_LEFTDOWN);
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(100);

            MoveMouse(to_x, to_y);
            Thread.Sleep(100);

            var up = CreateMouseInput(MOUSEEVENTF_LEFTUP);
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());

            return ToolResult.Ok($"dragged from ({from_x},{from_y}) to ({to_x},{to_y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.Scroll)]
    public static string scroll(int clicks)
    {
        return ToolResult.Run("scroll", () =>
        {
            var wheel = new INPUT
            {
                type = INPUT_MOUSE,
                u = new INPUTUNION
                {
                    mi = new MOUSEINPUT
                    {
                        mouseData = (uint)(clicks * WHEEL_DELTA),
                        dwFlags = MOUSEEVENTF_WHEEL
                    }
                }
            };
            SendInput(1, ref wheel, Marshal.SizeOf<INPUT>());

            var direction = clicks > 0 ? "up" : "down";
            return ToolResult.Ok($"scrolled {direction} {Math.Abs(clicks)} click(s) at current cursor position");
        });
    }

    [McpServerTool, Description(ToolDescriptions.MoveMouse)]
    public static string move_mouse(int x, int y)
    {
        return ToolResult.Run("move_mouse", () =>
        {
            MoveMouse(x, y);
            return ToolResult.Ok($"moved mouse to ({x},{y})");
        });
    }

    [McpServerTool, Description(ToolDescriptions.TypeText)]
    public static string type_text(string text)
    {
        return ToolResult.Run("type_text", () =>
        {
            var result = "";
            var t = new Thread(() =>
            {
                try
                {
                    System.Windows.Forms.SendKeys.SendWait(text);
                    result = ToolResult.Ok($"typed '{text}'");
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
        });
    }

    [McpServerTool, Description(ToolDescriptions.SendKey)]
    public static string send_key(string key)
    {
        return ToolResult.Run("send_key", () =>
        {
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

            var down = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = 0 }
                }
            };
            SendInput(1, ref down, Marshal.SizeOf<INPUT>());
            Thread.Sleep(50);

            var up = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new INPUTUNION
                {
                    ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = KEYEVENTF_KEYUP }
                }
            };
            SendInput(1, ref up, Marshal.SizeOf<INPUT>());

            return ToolResult.Ok($"sent '{key}' to focused window");
        });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void MoveMouse(int x, int y)
    {
        var input = new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION
            {
                mi = new MOUSEINPUT
                {
                    dx = x * 65536 / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Width,
                    dy = y * 65536 / System.Windows.Forms.Screen.PrimaryScreen!.Bounds.Height,
                    dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE
                }
            }
        };
        SendInput(1, ref input, Marshal.SizeOf<INPUT>());
    }

    private static INPUT CreateMouseInput(uint flags)
    {
        return new INPUT
        {
            type = INPUT_MOUSE,
            u = new INPUTUNION { mi = new MOUSEINPUT { dwFlags = flags } }
        };
    }

}

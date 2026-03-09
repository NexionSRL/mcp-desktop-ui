namespace McpDesktopUi.Common;

/// <summary>
/// Interface defining the 16 desktop UI automation tools.
/// Implemented per-platform in McpDesktopUi.Windows and McpDesktopUi.MacOS.
/// </summary>
public interface IUiTools
{
    string screenshot(string? window_title = null);
    string list_windows();
    string get_ui_tree(string window_title);
    string click_element(string window_title, string element_name);
    string click_at(int x, int y);
    string right_click_at(int x, int y);
    string double_click_at(int x, int y);
    string drag(int from_x, int from_y, int to_x, int to_y);
    string scroll(string window_title, int clicks);
    string scroll_at(int x, int y, int clicks);
    string move_mouse(int x, int y);
    string type_text(string window_title, string text);
    string send_key(string window_title, string key);
    string focus_window(string window_title);
    string get_window_rect(string window_title);
    string close_window(string window_title);
}

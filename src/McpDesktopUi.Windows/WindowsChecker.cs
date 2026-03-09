using McpDesktopUi.Common;

namespace WindowsMcp;

public class WindowsChecker : IPlatformChecker
{
    public IReadOnlyList<string> CheckDependencies()
    {
        // Windows UI Automation is always available on Windows 10+.
        // No external dependencies required.
        return [];
    }
}

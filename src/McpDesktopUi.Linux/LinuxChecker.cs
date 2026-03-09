using System.Diagnostics;
using McpDesktopUi.Common;

namespace LinuxMcp;

public class LinuxChecker : IPlatformChecker
{
    public IReadOnlyList<string> CheckDependencies()
    {
        var warnings = new List<string>();

        // Check DISPLAY is set (X11 required)
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY")))
            warnings.Add("DISPLAY environment variable not set. X11 is required (Wayland is not supported).");

        // Check required tools
        CheckCommand(warnings, "xdotool", "xdotool (apt install xdotool / dnf install xdotool)");
        CheckCommand(warnings, "wmctrl", "wmctrl (apt install wmctrl / dnf install wmctrl)");

        // Check screenshot tool
        if (!IsCommandAvailable("import") && !IsCommandAvailable("scrot"))
            warnings.Add("No screenshot tool found. Install ImageMagick (import) or scrot.");

        // Check AT-SPI2 for UI tree
        if (!IsCommandAvailable("python3"))
            warnings.Add("python3 not found. Required for UI tree inspection via AT-SPI2.");

        return warnings;
    }

    private static void CheckCommand(List<string> warnings, string command, string installHint)
    {
        if (!IsCommandAvailable(command))
            warnings.Add($"'{command}' not found. Install: {installHint}");
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var psi = new ProcessStartInfo("which", command)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}

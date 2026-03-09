using System.Runtime.InteropServices;
using McpDesktopUi.Common;

namespace MacOsMcp;

public class MacOsChecker : IPlatformChecker
{
    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern bool AXIsProcessTrusted();

    public IReadOnlyList<string> CheckDependencies()
    {
        var warnings = new List<string>();

        try
        {
            if (!AXIsProcessTrusted())
                warnings.Add("Accessibility permission not granted. Go to System Settings > Privacy & Security > Accessibility and add this application.");
        }
        catch (Exception ex)
        {
            warnings.Add($"Could not check Accessibility permission: {ex.Message}");
        }

        return warnings;
    }
}

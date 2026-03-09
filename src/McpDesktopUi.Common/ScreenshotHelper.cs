using System.Text;
using ModelContextProtocol.Protocol;

namespace McpDesktopUi.Common;

/// <summary>
/// Shared screenshot save/encode logic used by all platforms.
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// Returns a CallToolResult with the image as an ImageContentBlock (proper MCP image type).
    /// If a screenshot directory is configured, also saves the file and includes the path.
    /// </summary>
    public static CallToolResult ToImageResult(byte[] imageBytes, string mimeType, string? windowTitle)
    {
        var dir = ScreenshotConfig.Dir;
        var content = new List<ContentBlock>();

        if (!string.IsNullOrEmpty(dir))
        {
            var ext = mimeType == "image/jpeg" ? ".jpg" : ".png";
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var label = string.IsNullOrEmpty(windowTitle) ? "screen" : SanitizeFileName(windowTitle);
            var fileName = $"{timestamp}_{label}{ext}";
            var filePath = Path.Combine(dir, fileName);
            File.WriteAllBytes(filePath, imageBytes);
            content.Add(new TextContentBlock { Text = $"Saved to {filePath}" });
        }

        content.Add(ImageContentBlock.FromBytes(imageBytes, mimeType));
        return new CallToolResult { Content = content };
    }

    /// <summary>
    /// Reads a temp image file and returns a CallToolResult with ImageContentBlock.
    /// Deletes the temp file after reading.
    /// </summary>
    public static CallToolResult ToImageResultFromFile(string tempPath, string mimeType, string? windowTitle)
    {
        var dir = ScreenshotConfig.Dir;

        if (!string.IsNullOrEmpty(dir))
        {
            var destPath = Path.Combine(dir, Path.GetFileName(tempPath));
            File.Move(tempPath, destPath, overwrite: true);
            var savedBytes = File.ReadAllBytes(destPath);
            var content = new List<ContentBlock>
            {
                new TextContentBlock { Text = $"Saved to {destPath}" },
                ImageContentBlock.FromBytes(savedBytes, mimeType)
            };
            return new CallToolResult { Content = content };
        }

        var bytes = File.ReadAllBytes(tempPath);
        File.Delete(tempPath);
        return new CallToolResult
        {
            Content = [ImageContentBlock.FromBytes(bytes, mimeType)]
        };
    }

    /// <summary>
    /// Generates a temp file path for a screenshot capture.
    /// </summary>
    public static string GenerateTempPath(string? windowTitle, string extension = ".png")
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        var label = string.IsNullOrEmpty(windowTitle) ? "screen" : SanitizeFileName(windowTitle);
        return Path.Combine(Path.GetTempPath(), $"{timestamp}_{label}{extension}");
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        var result = sb.ToString().Trim();
        return result.Length > 50 ? result[..50] : result;
    }
}

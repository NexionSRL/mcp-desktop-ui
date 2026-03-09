using McpDesktopUi.Common;
using ModelContextProtocol.Protocol;
using Xunit;

namespace McpDesktopUi.Common.Tests;

public class ScreenshotHelperTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _originalDir;

    public ScreenshotHelperTests()
    {
        _originalDir = ScreenshotConfig.Dir;
        _tempDir = Path.Combine(Path.GetTempPath(), $"mcp_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        ScreenshotConfig.Dir = _originalDir;
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void ToImageResult_WithNoDir_ReturnsImageContent()
    {
        ScreenshotConfig.Dir = null;
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic

        var result = ScreenshotHelper.ToImageResult(bytes, "image/jpeg", null);

        Assert.True(result.IsError is null or false);
        Assert.Single(result.Content);
        var imageBlock = Assert.IsType<ImageContentBlock>(result.Content[0]);
        Assert.Equal("image/jpeg", imageBlock.MimeType);
    }

    [Fact]
    public void ToImageResult_WithDir_SavesFileAndReturnsImage()
    {
        ScreenshotConfig.Dir = _tempDir;
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };

        var result = ScreenshotHelper.ToImageResult(bytes, "image/jpeg", "TestWindow");

        Assert.True(result.IsError is null or false);
        Assert.Equal(2, result.Content.Count); // TextContentBlock + ImageContentBlock
        Assert.IsType<TextContentBlock>(result.Content[0]);
        Assert.IsType<ImageContentBlock>(result.Content[1]);
        var files = Directory.GetFiles(_tempDir, "*.jpg");
        Assert.Single(files);
        Assert.Equal(bytes, File.ReadAllBytes(files[0]));
    }

    [Fact]
    public void ToImageResultFromFile_WithNoDir_ReturnsImageAndDeletesTemp()
    {
        ScreenshotConfig.Dir = null;
        var bytes = new byte[] { 1, 2, 3, 4 };
        var tempFile = Path.Combine(_tempDir, "temp_test.png");
        File.WriteAllBytes(tempFile, bytes);

        var result = ScreenshotHelper.ToImageResultFromFile(tempFile, "image/png", null);

        Assert.True(result.IsError is null or false);
        Assert.Single(result.Content);
        Assert.IsType<ImageContentBlock>(result.Content[0]);
        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public void ToImageResultFromFile_WithDir_MovesFile()
    {
        var destDir = Path.Combine(_tempDir, "dest");
        Directory.CreateDirectory(destDir);
        ScreenshotConfig.Dir = destDir;
        var bytes = new byte[] { 1, 2, 3 };
        var tempFile = Path.Combine(_tempDir, "temp_move.png");
        File.WriteAllBytes(tempFile, bytes);

        var result = ScreenshotHelper.ToImageResultFromFile(tempFile, "image/png", "MyWindow");

        Assert.True(result.IsError is null or false);
        Assert.Equal(2, result.Content.Count);
        Assert.False(File.Exists(tempFile));
        var destFiles = Directory.GetFiles(destDir, "*.png");
        Assert.Single(destFiles);
    }

    [Fact]
    public void GenerateTempPath_WithWindowTitle_ContainsSanitizedName()
    {
        var path = ScreenshotHelper.GenerateTempPath("My App / Window");
        Assert.EndsWith(".png", path);
    }

    [Fact]
    public void GenerateTempPath_WithJpgExtension_UsesJpg()
    {
        var path = ScreenshotHelper.GenerateTempPath("test", ".jpg");
        Assert.EndsWith(".jpg", path);
    }

    [Fact]
    public void GenerateTempPath_WithNull_ContainsScreen()
    {
        var path = ScreenshotHelper.GenerateTempPath(null);
        Assert.Contains("screen", Path.GetFileName(path));
    }

    [Fact]
    public void SanitizeFileName_RemovesInvalidChars()
    {
        var invalid = Path.GetInvalidFileNameChars();
        var input = "my" + invalid[0] + "file" + invalid[0] + "name";
        var result = ScreenshotHelper.SanitizeFileName(input);
        foreach (var c in result)
            Assert.DoesNotContain(c, invalid);
        Assert.Contains("_", result);
    }

    [Fact]
    public void SanitizeFileName_TruncatesLongNames()
    {
        var longName = new string('a', 100);
        var result = ScreenshotHelper.SanitizeFileName(longName);
        Assert.Equal(50, result.Length);
    }
}

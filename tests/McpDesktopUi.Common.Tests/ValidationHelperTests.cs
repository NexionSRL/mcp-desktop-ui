using McpDesktopUi.Common;
using Xunit;

namespace McpDesktopUi.Common.Tests;

public class ValidationHelperTests
{
    [Theory]
    [InlineData("escape", "escape")]
    [InlineData("esc", "escape")]
    [InlineData("ESC", "escape")]
    [InlineData("enter", "enter")]
    [InlineData("return", "enter")]
    [InlineData("RETURN", "enter")]
    [InlineData("tab", "tab")]
    [InlineData("TAB", "tab")]
    [InlineData("space", "space")]
    [InlineData("SPACE", "space")]
    public void NormalizeKey_ValidKeys_ReturnsNormalized(string input, string expected)
    {
        Assert.Equal(expected, ValidationHelper.NormalizeKey(input));
    }

    [Theory]
    [InlineData("backspace")]
    [InlineData("ctrl")]
    [InlineData("shift")]
    [InlineData("")]
    [InlineData("f1")]
    public void NormalizeKey_InvalidKeys_ReturnsNull(string input)
    {
        Assert.Null(ValidationHelper.NormalizeKey(input));
    }

    [Fact]
    public void UnsupportedKeyError_FormatsCorrectly()
    {
        var result = ValidationHelper.UnsupportedKeyError("backspace");
        Assert.Contains("backspace", result);
        Assert.Contains("Supported:", result);
    }
}

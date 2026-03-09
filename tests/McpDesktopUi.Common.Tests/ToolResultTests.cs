using McpDesktopUi.Common;
using Xunit;

namespace McpDesktopUi.Common.Tests;

public class ToolResultTests
{
    [Fact]
    public void Ok_FormatsCorrectly()
    {
        Assert.Equal("OK: clicked at (100,200)", ToolResult.Ok("clicked at (100,200)"));
    }

    [Fact]
    public void Error_WithMessage_FormatsCorrectly()
    {
        Assert.Equal("ERROR: click_at failed: something broke", ToolResult.Error("click_at", "something broke"));
    }

    [Fact]
    public void Error_WithException_FormatsCorrectly()
    {
        var ex = new InvalidOperationException("bad state");
        Assert.Equal("ERROR: screenshot failed: bad state", ToolResult.Error("screenshot", ex));
    }

    [Fact]
    public void Run_ReturnsActionResult_OnSuccess()
    {
        var result = ToolResult.Run("test_tool", () => ToolResult.Ok("it worked"));
        Assert.Equal("OK: it worked", result);
    }

    [Fact]
    public void Run_CatchesException_ReturnsError()
    {
        var result = ToolResult.Run("test_tool", () => throw new Exception("boom"));
        Assert.Equal("ERROR: test_tool failed: boom", result);
    }
}

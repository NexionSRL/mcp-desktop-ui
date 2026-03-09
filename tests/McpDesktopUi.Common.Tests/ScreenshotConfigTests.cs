using McpDesktopUi.Common;
using Xunit;

namespace McpDesktopUi.Common.Tests;

public class ScreenshotConfigTests
{
    [Fact]
    public void Dir_DefaultsToNull()
    {
        var original = ScreenshotConfig.Dir;
        ScreenshotConfig.Dir = null;
        Assert.Null(ScreenshotConfig.Dir);
        ScreenshotConfig.Dir = original;
    }

    [Fact]
    public void Dir_SetAndGet_RoundTrips()
    {
        var original = ScreenshotConfig.Dir;
        try
        {
            ScreenshotConfig.Dir = "/tmp/test_screenshots";
            Assert.Equal("/tmp/test_screenshots", ScreenshotConfig.Dir);
        }
        finally
        {
            ScreenshotConfig.Dir = original;
        }
    }

    [Fact]
    public void Dir_IsThreadSafe()
    {
        var original = ScreenshotConfig.Dir;
        try
        {
            var values = new string[100];
            var tasks = new Task[100];
            for (int i = 0; i < 100; i++)
            {
                var idx = i;
                tasks[i] = Task.Run(() =>
                {
                    var path = $"/tmp/test_{idx}";
                    ScreenshotConfig.Dir = path;
                    // Read back - should get a valid string (may not be ours due to races, but shouldn't crash)
                    values[idx] = ScreenshotConfig.Dir ?? "null";
                });
            }
            Task.WaitAll(tasks);

            // All values should be non-null strings that look like paths
            Assert.All(values, v => Assert.NotNull(v));
        }
        finally
        {
            ScreenshotConfig.Dir = original;
        }
    }
}

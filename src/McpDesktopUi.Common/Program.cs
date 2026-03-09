using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace McpDesktopUi.Common;

/// <summary>
/// Shared MCP server entry point. Called by platform-specific projects.
/// </summary>
public static class Program
{
    /// <summary>
    /// Configures and runs the MCP server with stdio transport.
    /// Handles --screenshot-dir CLI flag and MCP_SCREENSHOT_DIR env var.
    /// Sets the ScreenshotDir property on the platform UiTools class via the provided setter.
    /// </summary>
    public static async Task RunAsync(string[] args, Action<string?> setScreenshotDir)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(Assembly.GetEntryAssembly()!);

        // Configure screenshot directory from --screenshot-dir argument or MCP_SCREENSHOT_DIR env var
        var screenshotDir = Environment.GetEnvironmentVariable("MCP_SCREENSHOT_DIR");
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--screenshot-dir")
            {
                screenshotDir = args[i + 1];
                break;
            }
        }
        if (!string.IsNullOrEmpty(screenshotDir))
        {
            screenshotDir = Path.GetFullPath(screenshotDir);
            Directory.CreateDirectory(screenshotDir);
        }
        setScreenshotDir(screenshotDir);

        await builder.Build().RunAsync();
    }
}

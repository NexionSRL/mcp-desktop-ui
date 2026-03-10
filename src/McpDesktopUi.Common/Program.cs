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
    /// Handles --screenshot-dir CLI flag (default: ./tmp/screenshots).
    /// Optionally runs platform-specific dependency checks at startup.
    /// </summary>
    public static async Task RunAsync(string[] args, IPlatformChecker? checker = null)
    {
        // --version flag: print version and exit
        if (args.Contains("--version"))
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";
            Console.WriteLine($"mcp-desktop-ui {version}");
            return;
        }

        // Run platform checks and log warnings to stderr (stdout is MCP transport)
        if (checker != null)
        {
            var warnings = checker.CheckDependencies();
            foreach (var warning in warnings)
                Console.Error.WriteLine($"[mcp-desktop-ui] WARNING: {warning}");
        }

        var builder = Host.CreateApplicationBuilder(args);
        builder.Logging.ClearProviders();
        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly(Assembly.GetEntryAssembly()!);

        // Configure screenshot directory: --screenshot-dir arg or default ./tmp/screenshots
        var screenshotDir = Path.Combine(Directory.GetCurrentDirectory(), "tmp", "screenshots");
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--screenshot-dir")
            {
                screenshotDir = args[i + 1];
                break;
            }
        }

        screenshotDir = Path.GetFullPath(screenshotDir);
        Directory.CreateDirectory(screenshotDir);
        ScreenshotConfig.Dir = screenshotDir;

        await builder.Build().RunAsync();
    }
}

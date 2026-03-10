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
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "unknown";

        // --version flag
        if (args.Contains("--version") || args.Contains("-v"))
        {
            Console.WriteLine($"mcp-desktop-ui {version}");
            return;
        }

        // --help flag
        if (args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp(version);
            return;
        }

        // --list-tools flag
        if (args.Contains("--list-tools"))
        {
            PrintTools();
            return;
        }

        // --check-deps flag
        if (args.Contains("--check-deps"))
        {
            if (checker != null)
            {
                var warnings = checker.CheckDependencies();
                if (warnings.Count == 0)
                {
                    Console.WriteLine("All dependencies OK.");
                }
                else
                {
                    foreach (var warning in warnings)
                        Console.WriteLine($"WARNING: {warning}");
                }
            }
            else
            {
                Console.WriteLine("No platform checker available.");
            }
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

    private static void PrintHelp(string version)
    {
        Console.WriteLine($"""
        mcp-desktop-ui v{version} — MCP server for desktop automation (screenshot, click, type, scroll)

        USAGE:
          mcp-desktop-ui [OPTIONS]

        OPTIONS:
          --help, -h            Show this help message
          --version, -v         Print version and exit
          --list-tools          List all available MCP tools with descriptions
          --check-deps          Check platform dependencies and exit
          --screenshot-dir DIR  Set screenshot output directory (default: ./tmp/screenshots)

        EXAMPLES:
          mcp-desktop-ui                          Start the MCP server (stdio transport)
          mcp-desktop-ui --screenshot-dir /tmp     Use /tmp for screenshots
          mcp-desktop-ui --list-tools              Show available tools

        NOTES:
          This server communicates over stdio using the MCP protocol.
          All output to stdout is MCP transport — diagnostics go to stderr.
          Screenshot coordinates map 1:1 to click coordinates (no scaling needed).
        """);
    }

    private static void PrintTools()
    {
        var tools = new (string Name, string Description)[]
        {
            ("screenshot",     ToolDescriptions.Screenshot),
            ("click_at",       ToolDescriptions.ClickAt),
            ("right_click_at", ToolDescriptions.RightClickAt),
            ("double_click_at",ToolDescriptions.DoubleClickAt),
            ("drag",           ToolDescriptions.Drag),
            ("scroll",         ToolDescriptions.Scroll),
            ("move_mouse",     ToolDescriptions.MoveMouse),
            ("type_text",      ToolDescriptions.TypeText),
            ("send_key",       ToolDescriptions.SendKey),
        };

        Console.WriteLine("Available tools:");
        Console.WriteLine();
        foreach (var (name, desc) in tools)
        {
            Console.WriteLine($"  {name,-18} {desc}");
        }
    }
}

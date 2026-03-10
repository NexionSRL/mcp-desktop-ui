using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit;

namespace McpDesktopUi.Integration.Tests;

/// <summary>
/// Integration tests that start the MCP server as a subprocess and communicate via JSON-RPC over stdio.
/// These tests verify the MCP protocol layer works correctly.
/// </summary>
public class McpServerTests : IDisposable
{
    private readonly string? _binaryPath;

    public McpServerTests()
    {
        // Find the platform-appropriate binary
        _binaryPath = FindBinary();
    }

    public void Dispose() { }

    private static string? FindBinary()
    {
        // Look for published binary or debug build
        var baseDir = FindRepoRoot();
        if (baseDir == null) return null;

        string[] searchPaths;
        if (OperatingSystem.IsMacOS())
        {
            searchPaths = [
                Path.Combine(baseDir, "src/McpDesktopUi.MacOS/bin/Debug/net10.0/osx-arm64/mcp-desktop-ui"),
                Path.Combine(baseDir, "src/McpDesktopUi.MacOS/bin/Release/net10.0/osx-arm64/mcp-desktop-ui"),
                Path.Combine(baseDir, "bin/mcp-desktop-ui"),
            ];
        }
        else if (OperatingSystem.IsWindows())
        {
            searchPaths = [
                Path.Combine(baseDir, "src/McpDesktopUi.Windows/bin/Debug/net10.0-windows/win-x64/mcp-desktop-ui.exe"),
                Path.Combine(baseDir, "src/McpDesktopUi.Windows/bin/Release/net10.0-windows/win-x64/mcp-desktop-ui.exe"),
            ];
        }
        else
        {
            searchPaths = [
                Path.Combine(baseDir, "src/McpDesktopUi.Linux/bin/Debug/net10.0/linux-x64/mcp-desktop-ui"),
                Path.Combine(baseDir, "src/McpDesktopUi.Linux/bin/Release/net10.0/linux-x64/mcp-desktop-ui"),
            ];
        }

        return searchPaths.FirstOrDefault(File.Exists);
    }

    private static string? FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "mcp-desktop-ui.slnx")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    [Fact]
    public async Task Server_RespondsToInitialize()
    {
        if (_binaryPath == null)
        {
            // Binary not built yet — skip gracefully
            return;
        }

        using var proc = StartServer();
        try
        {
            // Send initialize request
            var initRequest = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new { name = "test-client", version = "1.0.0" }
                }
            };

            await SendMessage(proc, initRequest);
            var response = await ReadResponse(proc, TimeSpan.FromSeconds(10));

            Assert.NotNull(response);
            Assert.True(response.TryGetProperty("result", out var result), "Response should have 'result'");
            Assert.True(result.TryGetProperty("serverInfo", out _), "Result should have 'serverInfo'");
        }
        finally
        {
            proc.Kill();
        }
    }

    [Fact]
    public async Task Server_ListsAllTools()
    {
        if (_binaryPath == null)
        {
            // Binary not built yet — skip gracefully
            return;
        }

        using var proc = StartServer();
        try
        {
            await Initialize(proc);

            // Send tools/list request
            var listRequest = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { }
            };

            await SendMessage(proc, listRequest);
            var response = await ReadResponse(proc, TimeSpan.FromSeconds(10));

            Assert.NotNull(response);
            Assert.True(response.TryGetProperty("result", out var result));
            Assert.True(result.TryGetProperty("tools", out var tools));
            Assert.Equal(JsonValueKind.Array, tools.ValueKind);

            var toolNames = tools.EnumerateArray()
                .Select(t => t.GetProperty("name").GetString())
                .ToList();

            // All 9 tools should be present
            Assert.Contains("screenshot", toolNames);
            Assert.Contains("click_at", toolNames);
            Assert.Contains("right_click_at", toolNames);
            Assert.Contains("double_click_at", toolNames);
            Assert.Contains("drag", toolNames);
            Assert.Contains("scroll", toolNames);
            Assert.Contains("move_mouse", toolNames);
            Assert.Contains("type_text", toolNames);
            Assert.Contains("send_key", toolNames);
            Assert.Equal(9, toolNames.Count);
        }
        finally
        {
            proc.Kill();
        }
    }

    private Process StartServer()
    {
        var psi = new ProcessStartInfo(_binaryPath!)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        return Process.Start(psi) ?? throw new Exception("Failed to start server");
    }

    private async Task Initialize(Process proc)
    {
        var initRequest = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client", version = "1.0.0" }
            }
        };
        await SendMessage(proc, initRequest);
        await ReadResponse(proc, TimeSpan.FromSeconds(10));

        // Send initialized notification
        var initialized = new { jsonrpc = "2.0", method = "notifications/initialized" };
        await SendMessage(proc, initialized);
    }

    private static async Task SendMessage(Process proc, object message)
    {
        var json = JsonSerializer.Serialize(message);
        var content = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {content.Length}\r\n\r\n";

        await proc.StandardInput.WriteAsync(header);
        await proc.StandardInput.BaseStream.WriteAsync(content);
        await proc.StandardInput.BaseStream.FlushAsync();
    }

    private static async Task<JsonElement> ReadResponse(Process proc, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var reader = proc.StandardOutput;

        // Read Content-Length header
        var headerLine = await ReadLineAsync(reader, cts.Token);
        while (string.IsNullOrWhiteSpace(headerLine))
            headerLine = await ReadLineAsync(reader, cts.Token);

        if (!headerLine.StartsWith("Content-Length:"))
            throw new Exception($"Expected Content-Length header, got: {headerLine}");

        var contentLength = int.Parse(headerLine["Content-Length:".Length..].Trim());

        // Read empty line
        await ReadLineAsync(reader, cts.Token);

        // Read content
        var buffer = new char[contentLength];
        var totalRead = 0;
        while (totalRead < contentLength)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), cts.Token);
            if (read == 0) throw new Exception("Stream ended");
            totalRead += read;
        }

        var jsonString = new string(buffer, 0, totalRead);
        return JsonSerializer.Deserialize<JsonElement>(jsonString);
    }

    private static async Task<string> ReadLineAsync(StreamReader reader, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var buffer = new char[1];
        while (!ct.IsCancellationRequested)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, 1), ct);
            if (read == 0) throw new Exception("Stream ended");
            if (buffer[0] == '\n') return sb.ToString().TrimEnd('\r');
            sb.Append(buffer[0]);
        }
        throw new OperationCanceledException(ct);
    }
}

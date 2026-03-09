namespace McpDesktopUi.Common;

/// <summary>
/// Interface for platform-specific startup dependency checks.
/// Implementations verify that required permissions and tools are available.
/// </summary>
public interface IPlatformChecker
{
    /// <summary>
    /// Checks platform dependencies and returns a list of warnings/errors.
    /// An empty list means all checks passed.
    /// </summary>
    IReadOnlyList<string> CheckDependencies();
}

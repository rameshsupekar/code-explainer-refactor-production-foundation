using System.Collections.Generic;

namespace CodeExplainer.Core
{
    /// <summary>
    /// Options that control analysis behavior.
    /// </summary>
    public sealed record AnalysisOptions(
        string RootPath,
        string? OutputPath = null,
        string OutputFormat = "mermaid",
        string DiagramTypes = "architecture,modules,behavior,dependency,class,component,sequence",
        bool IncludeTestFiles = false,
        IReadOnlyList<string>? IgnorePatterns = null,
        string? WorkerPath = null,
        int TimeoutMs = 30000)
    {
        /// <summary>
        /// Parsed diagram types.
        /// </summary>
        public IReadOnlySet<string> ParsedDiagramTypes =>
            new HashSet<string>(
                (DiagramTypes ?? "architecture,modules,behavior,dependency,class,component,sequence").Split(','),
                System.StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Default ignore patterns for common build and dependency directories.
        /// </summary>
        public static readonly IReadOnlyList<string> DefaultIgnorePatterns = new[]
        {
            "node_modules",
            "bin",
            "obj",
            "dist",
            "build",
            ".git",
            ".vscode",
            ".idea",
            "packages",
            ".angular",
            "coverage",
            ".nyc_output"
        };
    }
}

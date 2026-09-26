using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Core.Abstractions
{
    /// <summary>
    /// Result of a single analyzer's execution.
    /// </summary>
    public sealed record AnalyzerResult(
        IReadOnlyList<CodeSymbol> Symbols,
        IReadOnlyList<CodeRelationship> Relationships,
        IReadOnlyList<AnalysisDiagnostic> Diagnostics);

    /// <summary>
    /// Context provided to an analyzer for analysis.
    /// </summary>
    public sealed record AnalysisContext(
        string RootPath,
        IReadOnlyList<CodeFile> Files,
        AnalysisOptions Options);

    /// <summary>
    /// Contract for code analyzers across all languages.
    /// </summary>
    public interface ICodeAnalyzer
    {
        /// <summary>
        /// Friendly name of the analyzer.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Analyzes code files and produces symbols, relationships, and diagnostics.
        /// </summary>
        Task<AnalyzerResult> AnalyzeAsync(
            AnalysisContext context,
            CancellationToken cancellationToken);
    }
}

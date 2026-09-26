using System.Collections.Generic;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Core.Abstractions
{
    /// <summary>
    /// Resolves relationships between symbols across all languages.
    /// </summary>
    public interface IRelationshipResolver
    {
        /// <summary>
        /// Builds deterministic relationships from symbols and their metadata.
        /// </summary>
        /// <remarks>
        /// Only creates relationships where evidence exists in symbol metadata.
        /// Unresolved references are returned as diagnostics, not guessed edges.
        /// </remarks>
        IEnumerable<CodeRelationship> ResolveRelationships(
            IEnumerable<CodeSymbol> symbols,
            out IEnumerable<AnalysisDiagnostic> diagnostics);
    }
}

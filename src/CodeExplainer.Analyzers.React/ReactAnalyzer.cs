using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;
using CodeExplainer.Analyzers.TypeScript;

namespace CodeExplainer.Analyzers.React
{
    /// <summary>
    /// Analyzes React components, hooks, and JSX.
    /// </summary>
    public class ReactAnalyzer
    {
        private readonly TypeScriptAnalyzer _tsAnalyzer;

        public ReactAnalyzer(TypeScriptAnalyzer tsAnalyzer)
        {
            _tsAnalyzer = tsAnalyzer ?? throw new ArgumentNullException(nameof(tsAnalyzer));
        }

        /// <summary>
        /// Analyzes React files for components, hooks, and JSX relationships.
        /// </summary>
        public async Task<IEnumerable<CodeSymbol>> AnalyzeAsync(
            string rootPath,
            IEnumerable<string> files)
        {
            // Delegate to TypeScript analyzer with React framework context
            return await _tsAnalyzer.AnalyzeAsync(
                rootPath,
                files,
                new[] { "react" }
            );
        }

        // TODO: Add React-specific helpers:
        // - Detect function and class components
        // - Extract props interfaces
        // - Identify custom hooks (useXxx functions)
        // - Parse useState, useEffect, useContext calls
        // - Extract JSX component references
        // - Analyze React Router routing
        // - Track Context providers and consumers
    }
}

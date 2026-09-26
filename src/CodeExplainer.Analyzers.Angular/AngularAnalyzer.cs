using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;
using CodeExplainer.Analyzers.TypeScript;

namespace CodeExplainer.Analyzers.Angular
{
    /// <summary>
    /// Analyzes Angular components, services, and decorators.
    /// </summary>
    public class AngularAnalyzer
    {
        private readonly TypeScriptAnalyzer _tsAnalyzer;

        public AngularAnalyzer(TypeScriptAnalyzer tsAnalyzer)
        {
            _tsAnalyzer = tsAnalyzer ?? throw new ArgumentNullException(nameof(tsAnalyzer));
        }

        /// <summary>
        /// Analyzes Angular files for components, services, directives, pipes, and modules.
        /// </summary>
        public async Task<IEnumerable<CodeSymbol>> AnalyzeAsync(
            string rootPath,
            IEnumerable<string> files)
        {
            // Delegate to TypeScript analyzer with Angular framework context
            return await _tsAnalyzer.AnalyzeAsync(
                rootPath,
                files,
                new[] { "angular" }
            );
        }

        // TODO: Add Angular-specific helpers:
        // - Extract @Component, @Injectable, @Directive, @Pipe, @NgModule
        // - Parse dependency injection from constructors
        // - Extract routes from routing modules
        // - Parse template URLs and selectors
    }
}

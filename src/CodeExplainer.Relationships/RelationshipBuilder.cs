using System;
using System.Collections.Generic;
using System.Linq;
using CodeExplainer.Core.Abstractions;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Relationships
{
    /// <summary>
    /// Builds relationships between symbols from multiple languages.
    /// </summary>
    public class RelationshipBuilder : IRelationshipResolver
    {
        /// <summary>
        /// Analyzes symbols and builds cross-language relationships.
        /// </summary>
        public IEnumerable<CodeRelationship> BuildRelationships(
            IEnumerable<CodeSymbol> symbols)
        {
            return ResolveRelationships(symbols, out _);
        }

        public IEnumerable<CodeRelationship> ResolveRelationships(
            IEnumerable<CodeSymbol> symbols,
            out IEnumerable<AnalysisDiagnostic> diagnostics)
        {
            ArgumentNullException.ThrowIfNull(symbols);
            var symbolList = symbols.OrderBy(symbol => symbol.Id, StringComparer.Ordinal).ToArray();
            var byName = symbolList
                .GroupBy(symbol => symbol.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            var relationships = new List<CodeRelationship>();
            var diagnosticList = new List<AnalysisDiagnostic>();

            foreach (var source in symbolList)
            {
                if (!source.Metadata.TryGetValue("dependencies", out var dependencyList))
                    continue;

                foreach (var dependency in dependencyList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var name = NormalizeTypeName(dependency);
                    var matches = byName.TryGetValue(name, out var candidates)
                        ? candidates.Where(candidate => candidate.Id != source.Id).ToArray()
                        : Array.Empty<CodeSymbol>();
                    if (matches.Length == 1)
                    {
                        var kind = source.Language.Equals("typescript", StringComparison.OrdinalIgnoreCase) &&
                            source.Kind is SymbolKind.Component or SymbolKind.Service
                                ? RelationshipKind.Injects
                                : RelationshipKind.Uses;
                        relationships.Add(new CodeRelationship(source.Id, matches[0].Id, kind, dependency));
                    }
                    else if (matches.Length > 1)
                    {
                        diagnosticList.Add(new AnalysisDiagnostic(
                            "CEREL001",
                            DiagnosticSeverity.Warning,
                            $"Dependency '{dependency}' on '{source.Name}' matches multiple analyzed symbols.",
                            source.FilePath,
                            source.Location));
                    }
                }
            }

            var backendEndpoints = symbolList.Where(symbol =>
                symbol.Kind == SymbolKind.Endpoint &&
                !symbol.Metadata.ContainsKey("requestStyle") &&
                symbol.Metadata.ContainsKey("httpMethod") &&
                symbol.Metadata.ContainsKey("route")).ToArray();
            var requests = symbolList.Where(symbol =>
                symbol.Kind == SymbolKind.Endpoint &&
                symbol.Metadata.ContainsKey("requestStyle") &&
                symbol.Metadata.ContainsKey("httpMethod") &&
                symbol.Metadata.ContainsKey("route")).ToArray();

            foreach (var request in requests)
            {
                var method = request.Metadata["httpMethod"];
                var route = NormalizeRoute(request.Metadata["route"]);
                var matches = backendEndpoints.Where(endpoint =>
                    endpoint.Metadata["httpMethod"].Equals(method, StringComparison.OrdinalIgnoreCase) &&
                    NormalizeRoute(endpoint.Metadata["route"]).Equals(route, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length == 1)
                {
                    relationships.Add(new CodeRelationship(
                        request.Id,
                        matches[0].Id,
                        RelationshipKind.RoutesTo,
                        $"{method.ToUpperInvariant()} /{route}"));
                }
                else if (matches.Length > 1)
                {
                    diagnosticList.Add(new AnalysisDiagnostic(
                        "CEREL002",
                        DiagnosticSeverity.Warning,
                        $"HTTP request '{method} /{route}' matches multiple backend endpoints.",
                        request.FilePath,
                        request.Location));
                }
                else
                {
                    diagnosticList.Add(new AnalysisDiagnostic(
                        "CEREL003",
                        DiagnosticSeverity.Info,
                        $"No analyzed backend endpoint matches HTTP request '{method} /{route}'.",
                        request.FilePath,
                        request.Location));
                }
            }

            diagnostics = diagnosticList
                .Distinct()
                .OrderBy(diagnostic => diagnostic.FilePath, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                .ToArray();
            return relationships
                .Distinct()
                .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.Kind)
                .ThenBy(relationship => relationship.Label, StringComparer.Ordinal)
                .ToArray();
        }

        private static string NormalizeTypeName(string typeName)
        {
            var genericStart = typeName.IndexOf('<');
            var name = genericStart >= 0 ? typeName[..genericStart] : typeName;
            return name.Trim().TrimEnd('?', '!').Split('.').Last();
        }

        private static string NormalizeRoute(string route) =>
            System.Text.RegularExpressions.Regex.Replace(
                route.Trim().Trim('/'),
                "\\{[^}]+\\}|\\[[^]]+\\]",
                "{}")
            .ToLowerInvariant();
    }
}

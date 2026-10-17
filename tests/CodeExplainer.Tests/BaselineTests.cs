using System;
using System.Collections.Generic;
using CodeExplainer.Core.Models;
using CodeExplainer.Core.Utilities;
using CodeExplainer.Export.Mermaid;
using Xunit;

namespace CodeExplainer.Tests;

public sealed class BaselineTests
{
    [Fact]
    public void CreateDiagramNodeId_IsDeterministic()
    {
        const string symbolId = "csharp:src/Example.cs:Example.Service";

        var first = StableIdentifier.CreateDiagramNodeId(symbolId);
        var second = StableIdentifier.CreateDiagramNodeId(symbolId);

        Assert.Equal(first, second);
        Assert.StartsWith("N", first);
    }

    [Fact]
    public void ExportDependencyDiagram_UsesModelSymbolsAndRelationships()
    {
        var model = new CodeModel(
            new List<CodeFile>(),
            new List<CodeSymbol>
            {
                CreateSymbol("frontend", "User Interface", SymbolKind.Component),
                CreateSymbol("api", "Conversion API", SymbolKind.Endpoint)
            },
            new List<CodeRelationship>
            {
                new("frontend", "api", RelationshipKind.Imports, "POST /convert")
            },
            new List<AnalysisDiagnostic>());

        var diagram = new MermaidExporter().ExportDependencyDiagram(model);

        Assert.Contains("User Interface", diagram, StringComparison.Ordinal);
        Assert.Contains("Conversion API", diagram, StringComparison.Ordinal);
        Assert.Contains("Imports", diagram, StringComparison.Ordinal);
    }

    private static CodeSymbol CreateSymbol(string id, string name, SymbolKind kind) => new(
        id,
        name,
        kind,
        "typescript",
        $"src/{id}.tsx",
        new SourceLocation(1, 1, 1, 1),
        new List<CodeMember>(),
        new Dictionary<string, string>());
}
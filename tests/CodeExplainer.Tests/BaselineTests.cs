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

    [Fact]
    public void ExportClassDiagram_EscapesStructuralTypeMembersWithoutLosingSignatureDetails()
    {
        const string structuralType = "Array<{ specifier: string; target?: string }>";
        var symbol = CreateSymbol("source-context", "SourceContext", SymbolKind.Class) with
        {
            Members = new List<CodeMember>
            {
                new("source-context:imports", "imports", MemberKind.Method,
                    new SourceLocation(1, 1, 1, 1), new List<string>(), structuralType)
            }
        };
        var model = new CodeModel(
            new List<CodeFile>(),
            new List<CodeSymbol> { symbol },
            new List<CodeRelationship>(),
            new List<AnalysisDiagnostic>());

        var diagram = new MermaidExporter().ExportClassDiagram(model);

        Assert.Contains("imports() Array&lt;&#123; specifier: string&#59; target?: string &#125;&gt;", diagram, StringComparison.Ordinal);
        Assert.DoesNotContain("{ specifier: string;", diagram, StringComparison.Ordinal);
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
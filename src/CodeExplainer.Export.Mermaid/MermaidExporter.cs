using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeExplainer.Core.Models;
using CodeExplainer.Core.Utilities;

namespace CodeExplainer.Export.Mermaid;

public sealed class MermaidExporter
{
    public string ExportDependencyDiagram(CodeModel model)
    {
        var sb = new StringBuilder("flowchart TB\n");
        var symbols = model.Symbols.OrderBy(symbol => symbol.Id, StringComparer.Ordinal).ToArray();
        var ids = symbols.Select(symbol => symbol.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var symbol in symbols)
            sb.AppendLine($"    {Node(symbol.Id)}[\"{Escape($"{symbol.Name} ({symbol.Kind})")}\"]");
        foreach (var relationship in model.Relationships
            .Where(relationship => ids.Contains(relationship.SourceId) && ids.Contains(relationship.TargetId))
            .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Kind))
            sb.AppendLine($"    {Node(relationship.SourceId)} -->|{Escape(RelationshipLabel(relationship))}| {Node(relationship.TargetId)}");
        return sb.ToString();
    }

    public string ExportClassDiagram(CodeModel model)
    {
        var sb = new StringBuilder("classDiagram\n");
        var symbols = model.Symbols
            .Where(symbol => symbol.Kind is SymbolKind.Class or SymbolKind.Interface or SymbolKind.Component or SymbolKind.Service or SymbolKind.Controller)
            .OrderBy(symbol => symbol.Id, StringComparer.Ordinal)
            .ToArray();
        var ids = symbols.Select(symbol => symbol.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            var id = Node(symbol.Id);
            sb.AppendLine($"    class {id}[\"{Escape(symbol.Name)}\"] {{");
            foreach (var member in symbol.Members.OrderBy(member => member.Name, StringComparer.Ordinal))
                sb.AppendLine($"        {Escape(member.Name)}({Escape(string.Join(", ", member.Parameters))}) {Escape(member.ReturnType ?? "void")}");
            sb.AppendLine("    }");
        }
        foreach (var relationship in model.Relationships.Where(relationship =>
            ids.Contains(relationship.SourceId) && ids.Contains(relationship.TargetId) &&
            relationship.Kind is RelationshipKind.Inherits or RelationshipKind.Implements or RelationshipKind.Uses or RelationshipKind.Injects or RelationshipKind.Renders)
            .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal))
            sb.AppendLine($"    {Node(relationship.SourceId)} --> {Node(relationship.TargetId)} : {relationship.Kind}");
        return sb.ToString();
    }

    public string ExportComponentDiagram(CodeModel model)
    {
        var sb = new StringBuilder("flowchart LR\n");
        var symbols = model.Symbols.OrderBy(symbol => symbol.Id, StringComparer.Ordinal).ToArray();
        var ids = symbols.Select(symbol => symbol.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var symbol in symbols)
            sb.AppendLine($"    {Node(symbol.Id)}[\"{Escape($"{symbol.Name} ({symbol.Kind})")}\"]");
        foreach (var relationship in model.Relationships
            .Where(relationship => ids.Contains(relationship.SourceId) && ids.Contains(relationship.TargetId))
            .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Kind))
            sb.AppendLine($"    {Node(relationship.SourceId)} -->|{Escape(RelationshipLabel(relationship))}| {Node(relationship.TargetId)}");
        return sb.ToString();
    }

    public string ExportSequenceDiagram(CodeModel model)
    {
        var sb = new StringBuilder("sequenceDiagram\n");
        var symbols = model.Symbols.OrderBy(symbol => symbol.Id, StringComparer.Ordinal).ToArray();
        var ids = symbols.Select(symbol => symbol.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var symbol in symbols)
            sb.AppendLine($"    participant {Node(symbol.Id)} as {Escape(symbol.Name)}");
        foreach (var relationship in model.Relationships
            .Where(relationship => ids.Contains(relationship.SourceId) && ids.Contains(relationship.TargetId))
            .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Kind))
            sb.AppendLine($"    {Node(relationship.SourceId)}->>{Node(relationship.TargetId)}: {Escape(RelationshipLabel(relationship))}");
        return sb.ToString();
    }

    private static string Node(string value) => StableIdentifier.CreateDiagramNodeId(value);
    private static string Escape(string value) => StableIdentifier.EscapeMermaidLabel(value);
    private static string RelationshipLabel(CodeRelationship relationship) =>
        string.IsNullOrWhiteSpace(relationship.Label)
            ? relationship.Kind.ToString()
            : $"{relationship.Kind}: {relationship.Label}";
}

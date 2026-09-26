using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Export.Mermaid;

public sealed class MermaidExporter
{
    public string ExportDependencyDiagram(CodeModel model)
    {
        var sb = new StringBuilder("graph TB\n");
        foreach (var symbol in model.Symbols.OrderBy(s => s.Id)) sb.AppendLine($"    {Node(symbol.Id)}[\"{Escape(symbol.Name)}\"]");
        foreach (var relationship in model.Relationships.Where(r => r.Kind is RelationshipKind.Imports or RelationshipKind.Uses or RelationshipKind.Injects or RelationshipKind.Renders or RelationshipKind.References))
            sb.AppendLine($"    {Node(relationship.SourceId)} -->|{relationship.Kind}| {Node(relationship.TargetId)}");
        return sb.ToString();
    }

    public string ExportClassDiagram(CodeModel model)
    {
        var sb = new StringBuilder("classDiagram\n");
        foreach (var symbol in model.Symbols.Where(s => s.Kind is SymbolKind.Class or SymbolKind.Interface or SymbolKind.Component or SymbolKind.Service or SymbolKind.Controller))
        {
            var id = Node(symbol.Id);
            sb.AppendLine($"    class {id} {{");
            foreach (var member in symbol.Members) sb.AppendLine($"        {member.Name}({string.Join(", ", member.Parameters)}) {member.ReturnType ?? "void"}");
            sb.AppendLine("    }");
        }
        foreach (var relationship in model.Relationships.Where(r => r.Kind is RelationshipKind.Inherits or RelationshipKind.Implements or RelationshipKind.Uses or RelationshipKind.Renders))
            sb.AppendLine($"    {Node(relationship.SourceId)} --> {Node(relationship.TargetId)} : {relationship.Kind}");
        return sb.ToString();
    }

    private static string Node(string value) => "N" + Math.Abs(value.GetHashCode()).ToString("X");
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

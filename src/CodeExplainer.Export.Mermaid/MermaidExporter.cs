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

    public string ExportArchitectureDiagram(CodeModel model, ProjectInfo? projectInfo = null)
    {
        var sb = new StringBuilder("flowchart TB\n");
        var ids = model.Symbols.Select(symbol => symbol.Id).ToHashSet(StringComparer.Ordinal);
        var architectureGroups = new[]
        {
            (Name: "Entry points", Label: "User and API entry points", Symbols: model.Symbols.Where(symbol =>
                symbol.Kind is SymbolKind.Controller or SymbolKind.Endpoint or SymbolKind.Component or SymbolKind.Hook or SymbolKind.ContextProvider).ToArray()),
            (Name: "Workers", Label: "Background workers", Symbols: model.Symbols.Where(symbol =>
                symbol.Metadata.TryGetValue("executionRole", out var role) && role == "background-worker").ToArray()),
            (Name: "Application modules", Label: "Application services and modules", Symbols: model.Symbols.Where(symbol =>
                symbol.Kind is not (SymbolKind.Controller or SymbolKind.Endpoint or SymbolKind.Component or SymbolKind.Hook or SymbolKind.ContextProvider) &&
                (!symbol.Metadata.TryGetValue("executionRole", out var role) || role != "background-worker")).ToArray())
        };

        foreach (var group in architectureGroups)
        {
            if (group.Symbols.Length == 0)
                continue;
            sb.AppendLine($"    subgraph {Node($"architecture:{group.Name}")}[\"{Escape(group.Label)}\"]");
            foreach (var symbol in group.Symbols.OrderBy(symbol => symbol.Id, StringComparer.Ordinal))
                sb.AppendLine($"        {Node(symbol.Id)}[\"{Escape($"{symbol.Name} ({symbol.Kind})")}\"]");
            sb.AppendLine("    end");
        }

        foreach (var relationship in model.Relationships
            .Where(relationship => ids.Contains(relationship.SourceId) && ids.Contains(relationship.TargetId))
            .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Kind))
            sb.AppendLine($"    {Node(relationship.SourceId)} -->|{Escape(RelationshipLabel(relationship))}| {Node(relationship.TargetId)}");

        var infrastructure = projectInfo?.Evidence
            .Where(item => item.Category is not "Configuration" and not "Runtime" and not "Language" and not "Framework")
            .DistinctBy(item => (item.Name, item.Category, item.SourcePath))
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<RepositoryEvidence>();
        if (infrastructure.Length > 0)
        {
            sb.AppendLine($"    subgraph {Node("architecture:detected-infrastructure")}[\"Detected platform and infrastructure\"]");
            foreach (var item in infrastructure)
            {
                var id = Node($"evidence:{item.Category}:{item.Name}:{item.SourcePath}");
                sb.AppendLine($"        {id}[\"{Escape($"{item.Name} ({item.Category}, {item.Confidence}) — {item.SourcePath}")}\"]");
            }
            sb.AppendLine("    end");
        }

        return sb.ToString();
    }

    public string ExportModuleDiagram(CodeModel model)
    {
        var sb = new StringBuilder("flowchart LR\n");
        var groups = model.Symbols
            .GroupBy(symbol => ModuleName(symbol, model), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        foreach (var group in groups)
        {
            sb.AppendLine($"    subgraph {Node($"module:{group.Key}")}[\"{Escape(group.Key)}\"]");
            foreach (var symbol in group.OrderBy(symbol => symbol.Id, StringComparer.Ordinal))
                sb.AppendLine($"        {Node(symbol.Id)}[\"{Escape($"{symbol.Name} ({symbol.Kind})")}\"]");
            sb.AppendLine("    end");
        }

        var ids = model.Symbols.Select(symbol => symbol.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var relationship in model.Relationships
            .Where(relationship => ids.Contains(relationship.SourceId) && ids.Contains(relationship.TargetId))
            .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.Kind))
            sb.AppendLine($"    {Node(relationship.SourceId)} -->|{Escape(RelationshipLabel(relationship))}| {Node(relationship.TargetId)}");
        return sb.ToString();
    }

    public string ExportBehaviorDiagram(CodeModel model, IReadOnlyList<BehaviorFeature> features)
    {
        var sb = new StringBuilder("flowchart TB\n");
        var symbolsById = model.Symbols.ToDictionary(symbol => symbol.Id, StringComparer.Ordinal);
        for (var featureIndex = 0; featureIndex < features.Count; featureIndex++)
        {
            var feature = features[featureIndex];
            var featureId = Node($"behavior-feature:{feature.SourcePath}:{featureIndex}");
            sb.AppendLine($"    subgraph {featureId}[\"{Escape($"Feature: {feature.Name}") }\"]");
            for (var scenarioIndex = 0; scenarioIndex < feature.Scenarios.Count; scenarioIndex++)
            {
                var scenario = feature.Scenarios[scenarioIndex];
                var scenarioId = Node($"behavior-scenario:{feature.SourcePath}:{scenarioIndex}");
                sb.AppendLine($"        subgraph {scenarioId}[\"{Escape($"Scenario: {scenario.Name}") }\"]");
                var steps = scenario.Steps.Count == 0 ? new[] { "No steps found" } : scenario.Steps;
                var stepIds = new List<string>();
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    var stepId = Node($"behavior-step:{feature.SourcePath}:{scenarioIndex}:{stepIndex}");
                    stepIds.Add(stepId);
                    sb.AppendLine($"            {stepId}[\"{Escape(steps[stepIndex])}\"]");
                    if (stepIndex > 0)
                        sb.AppendLine($"            {stepIds[stepIndex - 1]} --> {stepId}");
                }
                sb.AppendLine("        end");
                if (stepIds.Count > 0)
                {
                    foreach (var symbolId in scenario.RelatedSymbolIds.Where(symbolsById.ContainsKey).OrderBy(id => id, StringComparer.Ordinal))
                    {
                        var symbol = symbolsById[symbolId];
                        sb.AppendLine($"        {Node(symbol.Id)}[\"{Escape($"Implementation: {symbol.Name}")}\"]");
                        sb.AppendLine($"        {stepIds[^1]} -. linked implementation .-> {Node(symbol.Id)}");
                    }
                }
            }
            sb.AppendLine("    end");
        }
        if (features.Count == 0 || features.All(feature => feature.Scenarios.Count == 0))
            sb.AppendLine("    noBehaviors[\"No Gherkin scenarios or detected endpoint drafts\"]");
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
    private static string ModuleName(CodeSymbol symbol, CodeModel model)
    {
        if (symbol.Metadata.TryGetValue("namespace", out var namespaceName) && !string.IsNullOrWhiteSpace(namespaceName))
            return $"Namespace: {namespaceName}";
        var file = model.Files.FirstOrDefault(file => file.RelativePath == symbol.FilePath);
        if (file?.ProjectPath != null)
            return $"Project: {System.IO.Path.GetFileNameWithoutExtension(file.ProjectPath)}";
        var directory = System.IO.Path.GetDirectoryName(symbol.FilePath)?.Replace('\\', '/');
        return string.IsNullOrWhiteSpace(directory) ? "Unassigned module" : $"Module: {directory}";
    }

    private static string RelationshipLabel(CodeRelationship relationship) =>
        string.IsNullOrWhiteSpace(relationship.Label)
            ? relationship.Kind.ToString()
            : $"{relationship.Kind}: {relationship.Label}";
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Export.Markdown
{
    /// <summary>
    /// Exports code models to Markdown documentation.
    /// </summary>
    public class MarkdownExporter
    {
        /// <summary>
        /// Exports a comprehensive markdown summary of the code model.
        /// </summary>
        public string ExportSummary(CodeModel model, ProjectInfo? projectInfo = null)
        {
            ArgumentNullException.ThrowIfNull(model);
            var sb = new StringBuilder();
            sb.AppendLine("# Architecture Summary\n");

            sb.AppendLine("## Overview\n");
            sb.AppendLine($"- Source files: {model.Files.Count}");
            sb.AppendLine($"- Symbols: {model.Symbols.Count}");
            sb.AppendLine($"- Relationships: {model.Relationships.Count}");
            sb.AppendLine($"- Diagnostics: {model.Diagnostics.Count}");
            var languages = model.Files.Select(file => file.Language).Distinct(StringComparer.Ordinal).OrderBy(language => language, StringComparer.Ordinal).ToArray();
            sb.AppendLine($"- Languages detected: {(languages.Length == 0 ? "none" : string.Join(", ", languages))}");

            if (projectInfo != null)
            {
                sb.AppendLine("\n## Project context\n");
                if (!string.IsNullOrWhiteSpace(projectInfo.SolutionPath))
                    sb.AppendLine($"- Solution: {Path.GetFileName(projectInfo.SolutionPath)}");
                foreach (var projectPath in projectInfo.ProjectPaths.OrderBy(path => path, StringComparer.Ordinal))
                    sb.AppendLine($"- Project: {Path.GetFileName(projectPath)}");
                foreach (var configPath in projectInfo.TypeScriptConfigPaths.OrderBy(path => path, StringComparer.Ordinal))
                    sb.AppendLine($"- TypeScript config: {Path.GetFileName(configPath)}");
            }

            sb.AppendLine("\n## Technology and infrastructure evidence\n");
            if (projectInfo?.Evidence.Count > 0)
            {
                foreach (var evidence in projectInfo.Evidence
                    .OrderBy(item => item.Category, StringComparer.Ordinal)
                    .ThenBy(item => item.Name, StringComparer.Ordinal)
                    .ThenBy(item => item.SourcePath, StringComparer.Ordinal))
                {
                    sb.AppendLine($"- **{EscapeMarkdown(evidence.Name)}** ({EscapeMarkdown(evidence.Category)}, {EscapeMarkdown(evidence.Confidence)}) — `{EscapeMarkdown(evidence.SourcePath)}`; {EscapeMarkdown(evidence.EvidenceType)}");
                }
            }
            else
            {
                sb.AppendLine("No technology or infrastructure evidence was found in recognized repository manifests.");
            }

            if (projectInfo?.ConfigurationPaths.Count > 0)
            {
                sb.AppendLine("\n### Configuration sources\n");
                foreach (var configPath in projectInfo.ConfigurationPaths.OrderBy(path => path, StringComparer.Ordinal))
                    sb.AppendLine($"- `{EscapeMarkdown(configPath)}` (values are not copied into this report)");
            }

            sb.AppendLine("\n## Source files\n");
            foreach (var file in model.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
                sb.AppendLine($"- `{file.RelativePath}` ({file.Language}, {file.LineCount} lines)");

            sb.AppendLine("\n## Symbols\n");
            foreach (var group in model.Symbols
                .OrderBy(symbol => symbol.Kind)
                .ThenBy(symbol => symbol.Name, StringComparer.Ordinal)
                .GroupBy(symbol => symbol.Kind))
            {
                sb.AppendLine($"### {group.Key}\n");
                foreach (var symbol in group)
                    sb.AppendLine($"- **{symbol.Name}** ({symbol.Language}) — `{symbol.FilePath}:{symbol.Location.StartLine}`");
                sb.AppendLine();
            }

            sb.AppendLine("## Relationships\n");
            var symbolsById = model.Symbols.ToDictionary(symbol => symbol.Id, StringComparer.Ordinal);
            foreach (var relationship in model.Relationships
                .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
                .ThenBy(relationship => relationship.Kind))
            {
                var sourceName = symbolsById.TryGetValue(relationship.SourceId, out var source) ? source.Name : relationship.SourceId;
                var targetName = symbolsById.TryGetValue(relationship.TargetId, out var target) ? target.Name : relationship.TargetId;
                var label = string.IsNullOrWhiteSpace(relationship.Label) ? string.Empty : $" — {relationship.Label}";
                sb.AppendLine($"- **{sourceName}** — {relationship.Kind}{label} → **{targetName}**");
            }
            if (model.Relationships.Count == 0)
                sb.AppendLine("No relationships identified.");

            sb.AppendLine("\n## BDD behavior and implementation traceability\n");
            var behaviorFeatures = projectInfo?.BehaviorFeatures ?? Array.Empty<BehaviorFeature>();
            if (behaviorFeatures.Count == 0)
            {
                sb.AppendLine("No Gherkin feature files or endpoint-backed behavior drafts were found. Static analysis cannot establish complete business behavior; add authored `.feature` files for authoritative scenarios.");
            }
            else
            {
                if (behaviorFeatures.Any(feature => feature.IsGeneratedDraft))
                    sb.AppendLine("Generated scenarios are review-required drafts based on detected code routes, not verified business requirements. Authored Gherkin remains authoritative.\n");
                sb.AppendLine($"- Features: {behaviorFeatures.Count}");
                sb.AppendLine($"- Scenarios: {behaviorFeatures.Sum(feature => feature.Scenarios.Count)}");
                sb.AppendLine($"- Draft scenarios: {behaviorFeatures.Sum(feature => feature.Scenarios.Count(scenario => scenario.IsGeneratedDraft))}");
                foreach (var feature in behaviorFeatures.OrderBy(item => item.SourcePath, StringComparer.Ordinal))
                {
                    sb.AppendLine($"\n### {EscapeMarkdown(feature.Name)}{(feature.IsGeneratedDraft ? " (generated draft)" : string.Empty)}\n");
                    sb.AppendLine($"Source: `{EscapeMarkdown(feature.SourcePath)}`\n");
                    foreach (var scenario in feature.Scenarios)
                    {
                        var scenarioLabel = EscapeMarkdown(scenario.Name);
                        if (scenario.IsGeneratedDraft)
                            scenarioLabel += " (draft; review required)";
                        sb.AppendLine($"- **{scenarioLabel}**");
                        var related = scenario.RelatedSymbolIds
                            .Where(symbolsById.ContainsKey)
                            .Select(id => symbolsById[id])
                            .Select(symbol => $"`{EscapeMarkdown(symbol.Name)}` ({EscapeMarkdown(symbol.FilePath)}:{symbol.Location.StartLine})")
                            .ToArray();
                        sb.AppendLine(related.Length == 0
                            ? "  - Implementation link: not established from available evidence."
                            : $"  - Implementation: {string.Join(", ", related)}");
                        if (scenario.Tags.Count > 0)
                            sb.AppendLine($"  - Tags: {string.Join(", ", scenario.Tags.Select(EscapeMarkdown))}");
                    }
                }
            }

            sb.AppendLine("\n## Diagnostics\n");
            foreach (var diagnostic in model.Diagnostics)
                sb.AppendLine($"- {diagnostic.Severity} `{diagnostic.Code}`: {diagnostic.Message}");
            if (model.Diagnostics.Count == 0)
                sb.AppendLine("No diagnostics.");

            return sb.ToString();
        }

        private static string EscapeMarkdown(string value) =>
            value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}

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

            sb.AppendLine("\n## Diagnostics\n");
            foreach (var diagnostic in model.Diagnostics)
                sb.AppendLine($"- {diagnostic.Severity} `{diagnostic.Code}`: {diagnostic.Message}");
            if (model.Diagnostics.Count == 0)
                sb.AppendLine("No diagnostics.");

            return sb.ToString();
        }
    }
}

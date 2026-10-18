using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Analyzers.CSharp;
using CodeExplainer.Analyzers.TypeScript;
using CodeExplainer.Core;
using CodeExplainer.Core.Models;
using CodeExplainer.Export.Markdown;
using CodeExplainer.Export.Mermaid;
using CodeExplainer.Export.PlantUml;
using CodeExplainer.Relationships;
using CodeExplainer.Scanner;

namespace CodeExplainer.Cli
{
    public sealed record AnalysisPipelineResult(CodeModel Model, IReadOnlyList<string> OutputFiles);

    public sealed class AnalysisPipeline
    {
        private static readonly HashSet<string> SupportedDiagrams = new(StringComparer.OrdinalIgnoreCase)
        {
            "architecture", "modules", "behavior", "dependency", "class", "component", "sequence"
        };

        public async Task<AnalysisPipelineResult> RunAsync(
            AnalysisOptions options,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            var format = options.OutputFormat?.Trim().ToLowerInvariant();
            if (format is not ("mermaid" or "plantuml"))
                throw new ArgumentException($"Unsupported output format '{options.OutputFormat}'. Use mermaid or plantuml.", nameof(options));

            var diagramTypes = (options.DiagramTypes ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var unsupportedDiagrams = diagramTypes.Where(type => !SupportedDiagrams.Contains(type)).ToArray();
            if (unsupportedDiagrams.Length > 0)
                throw new ArgumentException($"Unsupported diagram type(s): {string.Join(", ", unsupportedDiagrams)}.", nameof(options));
            if (diagramTypes.Length == 0)
                throw new ArgumentException("At least one diagram type is required.", nameof(options));

            var outputDirectory = Path.GetFullPath(options.OutputPath ?? "./generated");
            var scanOptions = options with { OutputPath = outputDirectory };
            var scanner = new FileScanner();
            var files = (await scanner.DiscoverFilesAsync(options.RootPath, scanOptions, cancellationToken).ConfigureAwait(false))
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();
            var rootPath = ResolveRootDirectory(options.RootPath);
            var projectInfo = await scanner.DiscoverProjectAsync(options.RootPath, cancellationToken).ConfigureAwait(false);
            var symbols = new List<CodeSymbol>();
            var relationships = new List<CodeRelationship>();
            var diagnostics = new List<AnalysisDiagnostic>();
            var csharpAnalyzer = new CSharpAnalyzer();

            foreach (var file in files.Where(file => file.Language == "csharp"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var source = await File.ReadAllTextAsync(file.FilePath, cancellationToken).ConfigureAwait(false);
                    symbols.AddRange(await csharpAnalyzer.AnalyzeAsync(file.RelativePath, source, cancellationToken).ConfigureAwait(false));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    diagnostics.Add(new AnalysisDiagnostic("CEFILE001", DiagnosticSeverity.Warning,
                        $"Could not read source file: {exception.Message}", file.RelativePath));
                }
            }

            var workerFiles = files.Where(file => Path.GetExtension(file.FilePath).ToLowerInvariant() is ".ts" or ".tsx" or ".js" or ".jsx" or ".html").ToArray();
            if (workerFiles.Any(file => Path.GetExtension(file.FilePath).ToLowerInvariant() is ".ts" or ".tsx" or ".js" or ".jsx"))
            {
                try
                {
                    var analyzer = new TypeScriptAnalyzer(options.WorkerPath ?? string.Empty, options.TimeoutMs);
                    var result = await analyzer.AnalyzeResultAsync(
                        rootPath,
                        workerFiles.Select(file => file.FilePath),
                        new[] { "typescript", "angular", "react" },
                        cancellationToken).ConfigureAwait(false);
                    symbols.AddRange(result.Symbols);
                    relationships.AddRange(result.Relationships);
                    diagnostics.AddRange(result.Diagnostics);
                }
                catch (Exception exception) when (exception is IOException or InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception)
                {
                    diagnostics.Add(new AnalysisDiagnostic("CEWORKER001", DiagnosticSeverity.Error,
                        $"TypeScript analysis could not run: {exception.Message}"));
                }
            }

            var resolver = new RelationshipBuilder();
            relationships.AddRange(resolver.ResolveRelationships(symbols, out var relationshipDiagnostics));
            diagnostics.AddRange(relationshipDiagnostics);
            if (files.Length == 0)
                diagnostics.Add(new AnalysisDiagnostic("CEFILE002", DiagnosticSeverity.Warning,
                    "No supported source files were found in the selected input."));

            var model = new CodeModel(
                files,
                symbols.OrderBy(symbol => symbol.Id, StringComparer.Ordinal).ToArray(),
                relationships.Distinct()
                    .OrderBy(relationship => relationship.SourceId, StringComparer.Ordinal)
                    .ThenBy(relationship => relationship.TargetId, StringComparer.Ordinal)
                    .ThenBy(relationship => relationship.Kind)
                    .ThenBy(relationship => relationship.Label, StringComparer.Ordinal)
                    .ToArray(),
                diagnostics.Distinct()
                    .OrderBy(diagnostic => diagnostic.FilePath, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                    .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
                    .ToArray());

            var behaviorFeatures = await new BehaviorFeatureBuilder()
                .BuildAsync(rootPath, outputDirectory, projectInfo, model, cancellationToken)
                .ConfigureAwait(false);
            if (projectInfo != null)
                projectInfo = projectInfo with { BehaviorFeatures = behaviorFeatures };
            else if (behaviorFeatures.Count > 0)
                projectInfo = new ProjectInfo(null, Array.Empty<string>(), Array.Empty<string>())
                {
                    BehaviorFeatures = behaviorFeatures
                };

            Directory.CreateDirectory(outputDirectory);
            var outputFiles = new List<string>();
            var extension = format == "mermaid" ? ".mmd" : ".puml";
            foreach (var diagramType in diagramTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var content = ExportDiagram(format, diagramType, model, projectInfo, behaviorFeatures);
                var outputFile = Path.Combine(outputDirectory, $"{diagramType.ToLowerInvariant()}{extension}");
                await File.WriteAllTextAsync(outputFile, content, cancellationToken).ConfigureAwait(false);
                outputFiles.Add(outputFile);
            }

            foreach (var feature in behaviorFeatures.Where(feature => feature.IsGeneratedDraft))
            {
                var relativePath = feature.SourcePath.Replace('/', Path.DirectorySeparatorChar);
                var outputFile = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));
                var relativeOutputPath = Path.GetRelativePath(outputDirectory, outputFile);
                if (Path.IsPathRooted(relativeOutputPath) || relativeOutputPath == ".." ||
                    relativeOutputPath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    throw new InvalidOperationException("Generated behavior path is outside the output directory.");
                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                await File.WriteAllTextAsync(outputFile, BehaviorFeatureBuilder.WriteGherkin(feature), cancellationToken).ConfigureAwait(false);
                outputFiles.Add(outputFile);
            }

            var summaryFile = Path.Combine(outputDirectory, "architecture.md");
            await File.WriteAllTextAsync(summaryFile, new MarkdownExporter().ExportSummary(model, projectInfo), cancellationToken).ConfigureAwait(false);
            outputFiles.Add(summaryFile);
            return new AnalysisPipelineResult(model, outputFiles);
        }

        private static string ExportDiagram(
            string format,
            string diagramType,
            CodeModel model,
            ProjectInfo? projectInfo,
            IReadOnlyList<BehaviorFeature> behaviorFeatures)
        {
            if (format == "mermaid")
            {
                var exporter = new MermaidExporter();
                return diagramType.ToLowerInvariant() switch
                {
                    "architecture" => exporter.ExportArchitectureDiagram(model, projectInfo),
                    "modules" => exporter.ExportModuleDiagram(model),
                    "behavior" => exporter.ExportBehaviorDiagram(model, behaviorFeatures),
                    "dependency" => exporter.ExportDependencyDiagram(model),
                    "class" => exporter.ExportClassDiagram(model),
                    "component" => exporter.ExportComponentDiagram(model),
                    "sequence" => exporter.ExportSequenceDiagram(model),
                    _ => throw new ArgumentOutOfRangeException(nameof(diagramType))
                };
            }

            var plantUmlExporter = new PlantUmlExporter();
            return diagramType.ToLowerInvariant() switch
            {
                "architecture" => plantUmlExporter.ExportArchitectureDiagram(model, projectInfo),
                "modules" => plantUmlExporter.ExportModuleDiagram(model),
                "behavior" => plantUmlExporter.ExportBehaviorDiagram(model, behaviorFeatures),
                "dependency" => plantUmlExporter.ExportDependencyDiagram(model),
                "class" => plantUmlExporter.ExportClassDiagram(model),
                "component" => plantUmlExporter.ExportComponentDiagram(model),
                "sequence" => plantUmlExporter.ExportSequenceDiagram(model),
                _ => throw new ArgumentOutOfRangeException(nameof(diagramType))
            };
        }

        private static string ResolveRootDirectory(string rootPath)
        {
            var fullPath = Path.GetFullPath(rootPath);
            return Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath)!;
        }
    }
}
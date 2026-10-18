using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Cli;
using CodeExplainer.Core;
using CodeExplainer.Core.Models;
using CodeExplainer.Scanner;
using Xunit;

namespace CodeExplainer.Tests;

public sealed class AnalysisPipelineTests
{
    [Theory]
    [InlineData("mermaid", ".mmd")]
    [InlineData("plantuml", ".puml")]
    public async Task RunAsync_RepositoryFolder_WritesRelatedSymbolsAndAllRequestedDiagrams(
        string outputFormat,
        string diagramExtension)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainer-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(rootPath, "src");
        var outputDirectory = Path.Combine(rootPath, "generated");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "CatalogController.cs"),
            "namespace Example; public interface ICatalogService { } public class CatalogController { public CatalogController(ICatalogService service) { } }");

        try
        {
            var result = await new AnalysisPipeline().RunAsync(new AnalysisOptions(
                rootPath,
                outputDirectory,
                outputFormat,
                "dependency,class,component,sequence"));

            Assert.Single(result.Model.Files);
            Assert.Contains(result.Model.Symbols, symbol => symbol.Name == "CatalogController");
            Assert.Contains(result.Model.Relationships, relationship =>
                relationship.Kind == RelationshipKind.Uses && relationship.Label == "ICatalogService");
            Assert.Equal(5, result.OutputFiles.Count);
            Assert.All(result.OutputFiles, outputFile => Assert.True(File.Exists(outputFile)));

            var diagramFiles = result.OutputFiles
                .Where(outputFile => Path.GetExtension(outputFile) == diagramExtension)
                .ToArray();
            Assert.Equal(4, diagramFiles.Length);
            var expectedHeaders = outputFormat == "mermaid"
                ? new[] { "flowchart TB", "classDiagram", "flowchart LR", "sequenceDiagram" }
                : Enumerable.Repeat("@startuml", 4).ToArray();
            for (var index = 0; index < diagramFiles.Length; index++)
            {
                var diagram = await File.ReadAllTextAsync(diagramFiles[index]);
                Assert.StartsWith(expectedHeaders[index], diagram, StringComparison.Ordinal);
                Assert.Contains("CatalogController", diagram, StringComparison.Ordinal);
                Assert.Contains("ICatalogService", diagram, StringComparison.Ordinal);
            }

            var summary = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "architecture.md"));
            Assert.Contains("CatalogController", summary, StringComparison.Ordinal);
            Assert.Contains("Uses", summary, StringComparison.Ordinal);
            Assert.Contains("ICatalogService", summary, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("mermaid", ".mmd")]
    [InlineData("plantuml", ".puml")]
    public async Task RunAsync_RepositoryWithBehaviorAndInfrastructure_WritesTraceableDocumentation(
        string outputFormat,
        string diagramExtension)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainer-{Guid.NewGuid():N}");
        var outputDirectory = Path.Combine(rootPath, "generated");
        var sourceDirectory = Path.Combine(rootPath, "src");
        var webDirectory = Path.Combine(rootPath, "web");
        var featureDirectory = Path.Combine(rootPath, "features");
        var infrastructureDirectory = Path.Combine(rootPath, "infra");
        Directory.CreateDirectory(sourceDirectory);
        Directory.CreateDirectory(webDirectory);
        Directory.CreateDirectory(featureDirectory);
        Directory.CreateDirectory(infrastructureDirectory);
        await File.WriteAllTextAsync(Path.Combine(rootPath, "Example.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "package.json"),
            "{\"dependencies\":{\"react\":\"18.0.0\",\"typescript\":\"5.0.0\"}}");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "tsconfig.json"), "{\"compilerOptions\":{\"strict\":true}}");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "appsettings.json"),
            "{\"ConnectionStrings\":{\"Redis\":\"must-not-appear-in-output\"}}");
        await File.WriteAllTextAsync(Path.Combine(infrastructureDirectory, "main.tf"),
            "resource \"azurerm_storage_account\" \"assets\" { name = \"example\" }");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "CatalogController.cs"),
            "namespace Example.Api; public class CatalogController { [Route(\"api/catalog\")] [HttpGet] public string Get() => \"items\"; }");
        await File.WriteAllTextAsync(Path.Combine(webDirectory, "CatalogClient.ts"),
            "export class CatalogClient { load(): Promise<string> { return Promise.resolve('catalog'); } }");
        await File.WriteAllTextAsync(Path.Combine(featureDirectory, "catalog.feature"),
            "Feature: Catalog API\n  Scenario: List catalog entries\n    When a GET request is sent to \"/api/catalog\"\n    Then GET /api/catalog returns catalog entries\n");

        try
        {
            var result = await new AnalysisPipeline().RunAsync(new AnalysisOptions(
                rootPath,
                outputDirectory,
                outputFormat,
                "architecture,modules,behavior",
                WorkerPath: FindTypeScriptWorkerDirectory()));

            Assert.Contains(result.Model.Files, file => file.Language == "typescript");
            Assert.Contains(result.Model.Symbols, symbol => symbol.Name == "CatalogController");
            Assert.Contains(result.Model.Symbols, symbol => symbol.Name == "CatalogClient");
            Assert.Contains(result.Model.Symbols, symbol => symbol.Kind == SymbolKind.Endpoint);

            var generatedFeaturePath = Path.Combine(outputDirectory, "behavior", "drafts", "api-endpoints.feature");
            Assert.True(File.Exists(generatedFeaturePath));
            var generatedFeature = await File.ReadAllTextAsync(generatedFeaturePath);
            Assert.Contains("@draft", generatedFeature, StringComparison.Ordinal);
            Assert.Contains("@review_required", generatedFeature, StringComparison.Ordinal);
            Assert.Contains("api/catalog", generatedFeature, StringComparison.Ordinal);

            var behaviorDiagram = await File.ReadAllTextAsync(Path.Combine(outputDirectory, $"behavior{diagramExtension}"));
            Assert.Contains(outputFormat == "mermaid" ? "flowchart TB" : "@startuml", behaviorDiagram, StringComparison.Ordinal);
            Assert.Contains("List catalog entries", behaviorDiagram, StringComparison.Ordinal);
            Assert.Contains("CatalogController", behaviorDiagram, StringComparison.Ordinal);
            var architectureDiagram = await File.ReadAllTextAsync(Path.Combine(outputDirectory, $"architecture{diagramExtension}"));
            Assert.Contains(outputFormat == "mermaid" ? "flowchart TB" : "@startuml", architectureDiagram, StringComparison.Ordinal);
            Assert.Contains("Azure resource azurerm_storage_account", architectureDiagram, StringComparison.Ordinal);
            var moduleDiagram = await File.ReadAllTextAsync(Path.Combine(outputDirectory, $"modules{diagramExtension}"));
            Assert.Contains(outputFormat == "mermaid" ? "flowchart LR" : "@startuml", moduleDiagram, StringComparison.Ordinal);
            Assert.Contains("Example.Api", moduleDiagram, StringComparison.Ordinal);

            var summary = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "architecture.md"));
            Assert.Contains("React", summary, StringComparison.Ordinal);
            Assert.Contains(".NET net10.0", summary, StringComparison.Ordinal);
            Assert.Contains("BDD behavior and implementation traceability", summary, StringComparison.Ordinal);
            Assert.Contains("CatalogController", summary, StringComparison.Ordinal);
            Assert.DoesNotContain("must-not-appear-in-output", summary, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_SolutionFile_UsesProjectSourcesAndIncludesProjectContext()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainer-{Guid.NewGuid():N}");
        var projectDirectory = Path.Combine(rootPath, "src");
        var outputDirectory = Path.Combine(rootPath, "generated");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Example.csproj");
        var solutionPath = Path.Combine(rootPath, "Example.sln");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        await File.WriteAllTextAsync(solutionPath, "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Example\", \"src\\Example.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject");
        await File.WriteAllTextAsync(Path.Combine(projectDirectory, "Example.cs"), "namespace Example; public class ExampleService { }");

        try
        {
            var result = await new AnalysisPipeline().RunAsync(new AnalysisOptions(
                solutionPath,
                outputDirectory,
                DiagramTypes: "dependency"));

            var sourceFile = Assert.Single(result.Model.Files);
            Assert.Equal("src/Example.cs", sourceFile.RelativePath);
            Assert.Contains(result.Model.Symbols, symbol => symbol.Name == "ExampleService");
            var summary = await File.ReadAllTextAsync(Path.Combine(outputDirectory, "architecture.md"));
            Assert.Contains("Solution: Example.sln", summary, StringComparison.Ordinal);
            Assert.Contains("Project: Example.csproj", summary, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(outputDirectory, "dependency.mmd")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private static string FindTypeScriptWorkerDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var workerDirectory = Path.Combine(directory.FullName, "workers", "typescript-analyzer");
            if (File.Exists(Path.Combine(workerDirectory, "dist", "index.js")))
                return workerDirectory;
        }

        return string.Empty;
    }

    [Fact]
    public async Task DiscoverFilesAsync_RepositoryFolder_IncludesSupportedLanguagesAndSkipsIgnoredFiles()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(rootPath, "src"));
        Directory.CreateDirectory(Path.Combine(rootPath, "web"));
        Directory.CreateDirectory(Path.Combine(rootPath, "tests"));
        Directory.CreateDirectory(Path.Combine(rootPath, "node_modules", "dependency"));
        await File.WriteAllTextAsync(Path.Combine(rootPath, "src", "App.cs"), "public class App { }");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "web", "app.ts"), "export const app = true;");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "web", "widget.tsx"), "export const Widget = () => null;");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "tests", "AppTests.cs"), "public class AppTests { }");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "node_modules", "dependency", "ignored.js"), "export const ignored = true;");

        try
        {
            var files = (await new FileScanner().DiscoverFilesAsync(
                rootPath,
                new AnalysisOptions(rootPath),
                CancellationToken.None)).ToArray();

            Assert.Contains(files, file => file.RelativePath == "src/App.cs" && file.Language == "csharp");
            Assert.Contains(files, file => file.RelativePath == "web/app.ts" && file.Language == "typescript");
            Assert.Contains(files, file => file.RelativePath == "web/widget.tsx" && file.Language == "typescript-react");
            Assert.DoesNotContain(files, file => file.RelativePath.StartsWith("tests/", StringComparison.Ordinal));
            Assert.DoesNotContain(files, file => file.RelativePath.Contains("node_modules", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }
}

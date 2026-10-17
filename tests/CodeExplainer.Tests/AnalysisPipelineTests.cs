using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Cli;
using CodeExplainer.Core;
using CodeExplainer.Scanner;
using Xunit;

namespace CodeExplainer.Tests;

public sealed class AnalysisPipelineTests
{
    [Fact]
    public async Task RunAsync_RepositoryFolder_WritesDiagramsAndArchitectureSummary()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainer-{Guid.NewGuid():N}");
        var sourceDirectory = Path.Combine(rootPath, "src");
        var outputDirectory = Path.Combine(rootPath, "generated");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "CatalogService.cs"),
            "namespace Example; public class CatalogService { public string GetName() => \"catalog\"; }");

        try
        {
            var result = await new AnalysisPipeline().RunAsync(new AnalysisOptions(
                rootPath,
                outputDirectory,
                DiagramTypes: "dependency,class,component,sequence"));

            Assert.Single(result.Model.Files);
            Assert.Contains(result.Model.Symbols, symbol => symbol.Name == "CatalogService");
            Assert.Equal(5, result.OutputFiles.Count);
            Assert.All(result.OutputFiles, outputFile => Assert.True(File.Exists(outputFile)));
            Assert.Contains("CatalogService", await File.ReadAllTextAsync(Path.Combine(outputDirectory, "architecture.md")));
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
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

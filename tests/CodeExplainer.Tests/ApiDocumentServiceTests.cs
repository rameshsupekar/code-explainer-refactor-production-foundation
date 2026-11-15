using CodeExplainer.Api.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace CodeExplainer.Tests;

public sealed class ApiDocumentServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsGuidePagesAndVisualFilesInReadingOrder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainerViewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(rootPath, "features", "catalog"));
        Directory.CreateDirectory(Path.Combine(rootPath, "node_modules", "ignored"));
        await File.WriteAllTextAsync(Path.Combine(rootPath, "features", "catalog", "README.md"), "# Catalog");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "architecture.md"), "# Architecture");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "dependency.mmd"), "flowchart LR\nA --> B");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "class.puml"), "@startuml\n@enduml");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "architecture.svg"), "<svg></svg>");
        await File.WriteAllBytesAsync(Path.Combine(rootPath, "class.png"), new byte[] { 1, 2, 3 });
        await File.WriteAllTextAsync(Path.Combine(rootPath, "node_modules", "ignored", "README.md"), "# Ignore");
        await File.WriteAllTextAsync(Path.Combine(rootPath, "source.cs"), "class Source {}");

        try
        {
            var documents = (await new LocalDiagramService().AnalyzeAsync(rootPath)).ToArray();

            Assert.Equal(
                new[] { "architecture.md", "README.md", "dependency.mmd", "architecture.svg", "class.puml", "class.png" },
                documents.Select(document => document.Name));
            Assert.Equal("features/catalog/README.md", documents[1].SourcePath);
            Assert.Equal("markdown", documents[0].Type);
            Assert.Equal("mermaid", documents[2].Type);
            Assert.Equal("svg", documents[3].Type);
            Assert.Equal("plantuml", documents[4].Type);
            Assert.StartsWith("data:image/png;base64,", documents[5].Content, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsNoPlaceholdersForEmptyFolder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainerViewer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(rootPath);

        try
        {
            var documents = await new LocalDiagramService().AnalyzeAsync(rootPath);

            Assert.Empty(documents);
        }
        finally
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task AnalyzeAsync_RejectsMissingFolder()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainerViewer-{Guid.NewGuid():N}");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            new LocalDiagramService().AnalyzeAsync(rootPath));
    }
}

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeExplainer.Cli;
using CodeExplainer.Core;
using CodeExplainer.Core.Models;
using Reqnroll;
using Xunit;

namespace CodeExplainer.Tests;

[Binding]
public sealed class CodeExplainerBddSteps
{
    private string _rootPath = string.Empty;
    private string _outputPath = string.Empty;
    private AnalysisPipelineResult? _result;
    private Exception? _error;

    [Given("a solution containing a C# project and source file")]
    public async Task GivenSolutionWithProjectAndSource()
    {
        CreateRepository();
        var sourceDirectory = Path.Combine(_rootPath, "src");
        Directory.CreateDirectory(sourceDirectory);
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "Example.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        await File.WriteAllTextAsync(Path.Combine(sourceDirectory, "Example.cs"), "namespace Example; public class CatalogService { }");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "Example.sln"),
            "Project(\"{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}\") = \"Example\", \"src\\Example.csproj\", \"{11111111-1111-1111-1111-111111111111}\"\nEndProject");
    }

    [When("I analyze the solution with the dependency diagram selected")]
    public async Task WhenAnalyzeSolutionWithDependencyDiagram() =>
        await RunPipelineAsync("dependency");

    [Then("the architecture summary identifies the solution and project")]
    public async Task ThenSummaryIdentifiesSolutionAndProject()
    {
        var summary = await ReadSummaryAsync();
        Assert.Contains("Solution: Example.sln", summary, StringComparison.Ordinal);
        Assert.Contains("Project: Example.csproj", summary, StringComparison.Ordinal);
    }

    [Then("the dependency diagram is written to the output directory")]
    public void ThenDependencyDiagramExists() =>
        Assert.True(File.Exists(Path.Combine(_outputPath, "dependency.mmd")));

    [Given("a repository containing supported C# and TypeScript source files")]
    public async Task GivenRepositoryWithDotNetAndTypeScriptSources()
    {
        CreateRepository();
        Directory.CreateDirectory(Path.Combine(_rootPath, "src"));
        Directory.CreateDirectory(Path.Combine(_rootPath, "web"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "src", "App.cs"), "public class BackendService { }");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "web", "catalog.ts"), "export class CatalogService { }");
    }

    [When("I analyze the repository with the TypeScript worker available")]
    public async Task WhenAnalyzeWithTypeScriptWorker()
    {
        var workerDirectory = FindTypeScriptWorkerDirectory();
        await RunPipelineAsync("dependency", workerDirectory);
    }

    [Then("symbols from both source ecosystems are included in the model")]
    public void ThenSymbolsFromBothEcosystemsExist()
    {
        Assert.NotNull(_result);
        Assert.Contains(_result!.Model.Symbols, symbol => symbol.Name == "BackendService");
        Assert.Contains(_result.Model.Symbols, symbol => symbol.Name == "CatalogService");
    }

    [Given("an empty repository")]
    public void GivenEmptyRepository() => CreateRepository();

    [When("I analyze the repository with the architecture diagram selected")]
    public async Task WhenAnalyzeWithArchitectureDiagram() =>
        await RunPipelineAsync("architecture");

    [Then("the result contains a no-supported-source diagnostic")]
    public void ThenNoSupportedSourceDiagnostic()
    {
        Assert.NotNull(_result);
        Assert.Contains(_result!.Model.Diagnostics, diagnostic => diagnostic.Code == "CEFILE002");
    }

    [Then("the architecture summary reports that no source files were analyzed")]
    public async Task ThenSummaryReportsNoSourceFiles() =>
        Assert.Contains("Source files: 0", await ReadSummaryAsync(), StringComparison.Ordinal);

    [Given("a repository containing a C# source file")]
    public async Task GivenRepositoryWithCSharpSource()
    {
        CreateRepository();
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "App.cs"), "public class App { }");
    }

    [When("I analyze the repository using an unsupported output format")]
    public async Task WhenAnalyzeWithUnsupportedFormat()
    {
        try
        {
            await new AnalysisPipeline().RunAsync(new AnalysisOptions(_rootPath, _outputPath, "unknown", "architecture"));
        }
        catch (Exception exception)
        {
            _error = exception;
        }
    }

    [Then("analysis fails with an unsupported-format validation error")]
    public void ThenUnsupportedFormatValidationError()
    {
        var exception = Assert.IsType<ArgumentException>(_error);
        Assert.Contains("Unsupported output format", exception.Message, StringComparison.Ordinal);
    }

    [Given("a repository containing application configuration and infrastructure manifests")]
    public async Task GivenRepositoryWithConfigurationAndInfrastructure()
    {
        CreateRepository();
        Directory.CreateDirectory(Path.Combine(_rootPath, "infra"));
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "appsettings.json"),
            "{\"ConnectionStrings\":{\"Redis\":\"bdd-secret-must-not-leak\"}}");
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "infra", "main.tf"),
            "resource \"azurerm_storage_account\" \"assets\" { name = \"assets\" }");
    }

    [Then("the architecture summary identifies evidenced technologies and resources")]
    public async Task ThenSummaryIdentifiesInfrastructureEvidence() =>
        Assert.Contains("Azure resource azurerm_storage_account", await ReadSummaryAsync(), StringComparison.Ordinal);

    [Then("configuration values are not copied into the summary")]
    public async Task ThenConfigurationSecretsAreNotCopied() =>
        Assert.DoesNotContain("bdd-secret-must-not-leak", await ReadSummaryAsync(), StringComparison.Ordinal);

    [Given("a repository containing a discoverable API endpoint")]
    public async Task GivenRepositoryWithApiEndpoint()
    {
        CreateRepository();
        await File.WriteAllTextAsync(Path.Combine(_rootPath, "CatalogController.cs"),
            "namespace Example; public class CatalogController { [Route(\"api/catalog\")] [HttpGet] public string Get() => \"items\"; }");
    }

    [When("I analyze the repository with the behavior diagram selected")]
    public async Task WhenAnalyzeWithBehaviorDiagram() =>
        await RunPipelineAsync("behavior");

    [Then("a Gherkin endpoint scenario draft is generated")]
    public async Task ThenEndpointDraftIsGenerated()
    {
        var draftPath = Path.Combine(_outputPath, "behavior", "drafts", "api-endpoints.feature");
        Assert.True(File.Exists(draftPath));
        Assert.Contains("api/catalog", await File.ReadAllTextAsync(draftPath), StringComparison.Ordinal);
    }

    [Then("the draft is marked as requiring review rather than an authoritative business requirement")]
    public async Task ThenDraftIsClearlyReviewRequired()
    {
        var draftPath = Path.Combine(_outputPath, "behavior", "drafts", "api-endpoints.feature");
        var draft = await File.ReadAllTextAsync(draftPath);
        Assert.Contains("@review_required", draft, StringComparison.Ordinal);
        Assert.Contains("review and replace with business outcomes", draft, StringComparison.Ordinal);
    }

    [AfterScenario]
    public void Cleanup()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private void CreateRepository()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), $"CodeExplainer-Bdd-{Guid.NewGuid():N}");
        _outputPath = Path.Combine(_rootPath, "generated");
        Directory.CreateDirectory(_rootPath);
    }

    private Task RunPipelineAsync(string diagramTypes, string? workerPath = null) =>
        RunPipelineCoreAsync(diagramTypes, workerPath);

    private async Task RunPipelineCoreAsync(string diagramTypes, string? workerPath)
    {
        _result = await new AnalysisPipeline().RunAsync(new AnalysisOptions(
            _rootPath,
            _outputPath,
            DiagramTypes: diagramTypes,
            WorkerPath: workerPath));
    }

    private Task<string> ReadSummaryAsync() =>
        File.ReadAllTextAsync(Path.Combine(_outputPath, "architecture.md"));

    private static string FindTypeScriptWorkerDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var workerDirectory = Path.Combine(directory.FullName, "workers", "typescript-analyzer");
            if (File.Exists(Path.Combine(workerDirectory, "dist", "index.js")))
                return workerDirectory;
        }

        throw new DirectoryNotFoundException("Build workers/typescript-analyzer before running the cross-language BDD scenario.");
    }
}

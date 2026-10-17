using System;
using System.Linq;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core;

namespace CodeExplainer.Cli
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Code architecture analyzer for C#, TypeScript, Angular, and React");
            var pathArgument = new Argument<string>("path")
            {
                Description = "Path to the repository folder, solution, or project to analyze"
            };
            var formatOption = new Option<string>("--format", "-f")
            {
                DefaultValueFactory = _ => "mermaid",
                Description = "Output format: mermaid or plantuml"
            };
            var outputOption = new Option<string>("--output", "-o")
            {
                DefaultValueFactory = _ => "./generated",
                Description = "Output directory for diagrams and the architecture summary"
            };
            var diagramOption = new Option<string>("--diagram", "-d")
            {
                DefaultValueFactory = _ => "dependency,class",
                Description = "Comma-separated diagram types: dependency, class, component, sequence"
            };
            var workerOption = new Option<string>("--worker")
            {
                Description = "Path to the TypeScript worker script, worker directory, or package.json"
            };

            var analyzeCommand = new Command("analyze", "Analyze code and generate architecture diagrams");
            analyzeCommand.Arguments.Add(pathArgument);
            analyzeCommand.Options.Add(formatOption);
            analyzeCommand.Options.Add(outputOption);
            analyzeCommand.Options.Add(diagramOption);
            analyzeCommand.Options.Add(workerOption);
            analyzeCommand.SetAction(async parseResult =>
            {
                using var cancellation = new CancellationTokenSource();
                ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cancellation.Cancel();
                };
                Console.CancelKeyPress += cancelHandler;
                try
                {
                    var options = new AnalysisOptions(
                        parseResult.GetValue(pathArgument) ?? throw new InvalidOperationException("A path is required."),
                        parseResult.GetValue(outputOption),
                        parseResult.GetValue(formatOption) ?? "mermaid",
                        parseResult.GetValue(diagramOption) ?? "dependency,class",
                        WorkerPath: parseResult.GetValue(workerOption));
                    var result = await new AnalysisPipeline().RunAsync(options, cancellation.Token).ConfigureAwait(false);
                    Console.WriteLine($"Analyzed {result.Model.Files.Count} source files and found {result.Model.Symbols.Count} symbols.");
                    foreach (var diagnostic in result.Model.Diagnostics)
                        Console.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
                    foreach (var outputFile in result.OutputFiles)
                        Console.WriteLine($"Wrote {outputFile}");
                    return result.Model.Diagnostics.Any(diagnostic => diagnostic.Severity == CodeExplainer.Core.Models.DiagnosticSeverity.Error)
                        ? 1
                        : 0;
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("Analysis canceled.");
                    return 130;
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine($"Analysis failed: {exception.Message}");
                    return 1;
                }
                finally
                {
                    Console.CancelKeyPress -= cancelHandler;
                }
            });

            rootCommand.Subcommands.Add(analyzeCommand);
            return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
        }
    }
}

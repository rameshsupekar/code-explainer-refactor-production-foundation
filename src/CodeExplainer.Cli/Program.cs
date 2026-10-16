using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace CodeExplainer.Cli
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Code architecture analyzer for C#, TypeScript, Angular, and React");

            var pathArgument = new Argument<DirectoryInfo>("path")
            {
                Description = "Path to the folder to analyze"
            };
            var formatOption = new Option<string>("--format", "-f")
            {
                DefaultValueFactory = _ => "mermaid",
                Description = "Output format: mermaid or plantuml"
            };
            var outputOption = new Option<string>("--output", "-o")
            {
                DefaultValueFactory = _ => "./generated",
                Description = "Output directory for diagrams"
            };
            var diagramOption = new Option<string>("--diagram", "-d")
            {
                DefaultValueFactory = _ => "dependency,class",
                Description = "Comma-separated diagram types: dependency, class, component, sequence"
            };

            var analyzeCommand = new Command("analyze", "Analyze code and generate architecture diagrams");
            analyzeCommand.Arguments.Add(pathArgument);
            analyzeCommand.Options.Add(formatOption);
            analyzeCommand.Options.Add(outputOption);
            analyzeCommand.Options.Add(diagramOption);

            analyzeCommand.SetAction(parseResult =>
            {
                try
                {
                    var path = parseResult.GetValue(pathArgument)
                        ?? throw new InvalidOperationException("A path is required.");
                    var format = parseResult.GetValue(formatOption);
                    var output = parseResult.GetValue(outputOption);
                    var diagram = parseResult.GetValue(diagramOption);

                    Console.WriteLine($"📁 Analyzing: {path.FullName}");
                    Console.WriteLine($"📊 Format: {format}");
                    Console.WriteLine($"📤 Output: {output}");
                    Console.WriteLine($"📈 Diagrams: {diagram}");
                    
                    // TODO: Implement analysis logic
                    Console.WriteLine("\n✅ Analysis complete!");
                    return 0;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"❌ Error: {ex.Message}");
                    return 1;
                }
            });

            rootCommand.Subcommands.Add(analyzeCommand);

            return await rootCommand.Parse(args).InvokeAsync();
        }
    }
}

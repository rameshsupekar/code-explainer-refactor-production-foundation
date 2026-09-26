using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace CodeExplainer.Cli
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Code architecture analyzer for C#, TypeScript, Angular, and React");

            var analyzeCommand = new Command("analyze", "Analyze code and generate architecture diagrams")
            {
                new Argument<DirectoryInfo>("path", "Path to the folder to analyze"),
                new Option<string>(
                    new[] { "--format", "-f" },
                    getDefaultValue: () => "mermaid",
                    description: "Output format: mermaid or plantuml"),
                new Option<string>(
                    new[] { "--output", "-o" },
                    getDefaultValue: () => "./generated",
                    description: "Output directory for diagrams"),
                new Option<string>(
                    new[] { "--diagram", "-d" },
                    getDefaultValue: () => "dependency,class",
                    description: "Comma-separated diagram types: dependency, class, component, sequence")
            };

            analyzeCommand.SetHandler(async (DirectoryInfo path, string format, string output, string diagram) =>
            {
                try
                {
                    Console.WriteLine($"📁 Analyzing: {path.FullName}");
                    Console.WriteLine($"📊 Format: {format}");
                    Console.WriteLine($"📤 Output: {output}");
                    Console.WriteLine($"📈 Diagrams: {diagram}");
                    
                    // TODO: Implement analysis logic
                    Console.WriteLine("\n✅ Analysis complete!");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"❌ Error: {ex.Message}");
                    Environment.Exit(1);
                }
            }, new Argument<DirectoryInfo>("path"), 
               new Option<string>(new[] { "--format", "-f" }),
               new Option<string>(new[] { "--output", "-o" }),
               new Option<string>(new[] { "--diagram", "-d" }));

            rootCommand.AddCommand(analyzeCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}

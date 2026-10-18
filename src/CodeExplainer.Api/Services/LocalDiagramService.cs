namespace CodeExplainer.Api.Services;

public interface IDiagramService
{
    Task<IEnumerable<DiagramDetail>> AnalyzeAsync(string path);
}

public sealed record DiagramDetail(string Name, string Type, string Content, string? SourcePath = null);

public sealed class LocalDiagramService : IDiagramService
{
    public Task<IEnumerable<DiagramDetail>> AnalyzeAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult<IEnumerable<DiagramDetail>>(Array.Empty<DiagramDetail>());

        var target = Path.GetFullPath(path);
        if (!Directory.Exists(target))
        {
            var fallback = new[]
            {
                new DiagramDetail("analysis-error", "text", $"Directory not found: {path}")
            };
            return Task.FromResult<IEnumerable<DiagramDetail>>(fallback);
        }

        var diagrams = new List<DiagramDetail>();

        var files = Directory.EnumerateFiles(target, "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".mmd", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".mermaid", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".puml", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".plantuml", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var extension = Path.GetExtension(file).ToLowerInvariant();

            if (extension is ".mmd" or ".mermaid")
            {
                diagrams.Add(new DiagramDetail(Path.GetFileName(file), "mermaid", content, file));
            }
            else if (extension is ".puml" or ".plantuml")
            {
                diagrams.Add(new DiagramDetail(Path.GetFileName(file), "plantuml", content, file));
            }
            else if (extension is ".svg")
            {
                diagrams.Add(new DiagramDetail(Path.GetFileName(file), "svg", content, file));
            }
        }

        if (diagrams.Count == 0)
        {
            diagrams.Add(new DiagramDetail("sample-mermaid", "mermaid", "graph LR\n    A[Input] --> B[Analyzer]\n    B --> C[Diagram]"));
            diagrams.Add(new DiagramDetail("sample-plantuml", "plantuml", "@startuml\nUser -> Analyzer: analyze\nAnalyzer -> Diagram: export\n@enduml"));
        }

        return Task.FromResult<IEnumerable<DiagramDetail>>(diagrams);
    }
}

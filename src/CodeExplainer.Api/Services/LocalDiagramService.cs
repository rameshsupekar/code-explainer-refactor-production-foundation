namespace CodeExplainer.Api.Services;

public interface IDiagramService
{
    Task<IEnumerable<DiagramDetail>> AnalyzeAsync(string path);
}

public sealed record DiagramDetail(string Name, string Type, string Content, string? SourcePath = null);

public sealed class LocalDiagramService : IDiagramService
{
    private const int MaximumDocumentCount = 1000;
    private const long MaximumDocumentBytes = 5 * 1024 * 1024;
    private const long MaximumTotalDocumentBytes = 20 * 1024 * 1024;
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", "node_modules", "bin", "obj", "dist", "build", ".angular"
    };

    public Task<IEnumerable<DiagramDetail>> AnalyzeAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var target = Path.GetFullPath(path);
        if (!Directory.Exists(target))
            throw new DirectoryNotFoundException($"Document directory not found: {path}");

        var documents = new List<DiagramDetail>();
        long totalDocumentBytes = 0;
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(target);

        while (pendingDirectories.TryPop(out var directory))
        {
            foreach (var subdirectory in Directory.EnumerateDirectories(directory))
            {
                if (!IgnoredDirectories.Contains(Path.GetFileName(subdirectory)))
                    pendingDirectories.Push(subdirectory);
            }

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (!TryGetDocumentType(extension, out var type))
                    continue;

                if (documents.Count == MaximumDocumentCount)
                    throw new InvalidDataException($"The document folder contains more than {MaximumDocumentCount} supported files.");

                var fileInfo = new FileInfo(file);
                if (fileInfo.Length > MaximumDocumentBytes)
                    throw new InvalidDataException($"Document exceeds the 5 MB limit: {Path.GetRelativePath(target, file)}");
                totalDocumentBytes += fileInfo.Length;
                if (totalDocumentBytes > MaximumTotalDocumentBytes)
                    throw new InvalidDataException("The selected guide exceeds the 20 MB total size limit.");

                var relativePath = Path.GetRelativePath(target, file).Replace('\\', '/');
                var content = type.StartsWith("image/", StringComparison.Ordinal)
                    ? $"data:{type};base64,{Convert.ToBase64String(File.ReadAllBytes(file))}"
                    : File.ReadAllText(file);
                documents.Add(new DiagramDetail(Path.GetFileName(file), type, content, relativePath));
            }
        }

        var orderedDocuments = documents
            .OrderBy(document => GetDocumentOrder(document), StringComparer.Ordinal)
            .ThenBy(document => document.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult<IEnumerable<DiagramDetail>>(orderedDocuments);
    }

    private static bool TryGetDocumentType(string extension, out string type)
    {
        type = extension switch
        {
            ".md" => "markdown",
            ".mmd" or ".mermaid" => "mermaid",
            ".puml" or ".plantuml" => "plantuml",
            ".svg" => "svg",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => string.Empty
        };
        return type.Length > 0;
    }

    private static string GetDocumentOrder(DiagramDetail document)
    {
        if (document.Type == "markdown" &&
            (string.Equals(Path.GetFileName(document.SourcePath), "architecture.md", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(Path.GetFileName(document.SourcePath), "README.md", StringComparison.OrdinalIgnoreCase)))
            return "0";
        return document.Type switch
        {
            "markdown" => "1",
            "mermaid" => "2",
            "svg" => "3",
            "plantuml" => "4",
            _ => "5"
        };
    }
}

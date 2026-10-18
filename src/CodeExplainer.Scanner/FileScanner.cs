using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CodeExplainer.Core;
using CodeExplainer.Core.Abstractions;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Scanner
{
    /// <summary>
    /// Discovers supported source files in a directory, project, or solution.
    /// </summary>
    public sealed class FileScanner : ISourceDiscovery
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".ts", ".tsx", ".js", ".jsx", ".html"
        };

        public async Task<IEnumerable<FileInfo>> ScanAsync(DirectoryInfo rootDirectory)
        {
            ArgumentNullException.ThrowIfNull(rootDirectory);
            var results = new List<FileInfo>();
            ScanDirectory(rootDirectory, results, AnalysisOptions.DefaultIgnorePatterns, CancellationToken.None);
            await Task.CompletedTask.ConfigureAwait(false);
            return results;
        }

        public async Task<IEnumerable<CodeFile>> DiscoverFilesAsync(
            string rootPath,
            AnalysisOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            ArgumentNullException.ThrowIfNull(options);

            var ignorePatterns = options.IgnorePatterns ?? AnalysisOptions.DefaultIgnorePatterns;
            var scope = ResolveScope(rootPath, ignorePatterns);
            var outputDirectory = string.IsNullOrWhiteSpace(options.OutputPath)
                ? null
                : Path.GetFullPath(options.OutputPath);
            var excludeOutputDirectory = outputDirectory != null &&
                !PathComparer.Equals(scope.RootPath, outputDirectory) &&
                IsWithinDirectory(outputDirectory, scope.RootPath);
            var paths = new Dictionary<string, FileInfo>(PathComparer);
            foreach (var directory in scope.ScanDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in EnumerateFiles(directory, ignorePatterns, cancellationToken))
                {
                    if (excludeOutputDirectory && IsWithinDirectory(file.FullName, outputDirectory!))
                        continue;
                    paths.TryAdd(file.FullName, file);
                }
            }

            var projectDirectories = scope.ProjectPaths
                .Select(projectPath => (Path: projectPath, Directory: Path.GetDirectoryName(projectPath)!))
                .ToArray();
            var files = new List<CodeFile>(paths.Count);
            foreach (var file in paths.Values.OrderBy(file => file.FullName, PathComparer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!options.IncludeTestFiles && IsTestFile(file, scope.RootPath))
                    continue;
                var relativePath = Path.GetRelativePath(scope.RootPath, file.FullName).Replace('\\', '/');
                var projectPath = projectDirectories
                    .Where(project => IsWithinDirectory(file.FullName, project.Directory))
                    .OrderByDescending(project => project.Directory.Length)
                    .Select(project => project.Path)
                    .FirstOrDefault();
                var lineCount = (await File.ReadAllLinesAsync(file.FullName, cancellationToken).ConfigureAwait(false)).Length;
                files.Add(new CodeFile(file.FullName, relativePath, GetLanguage(file), projectPath, lineCount));
            }

            return files;
        }

        public async Task<ProjectInfo?> DiscoverProjectAsync(string rootPath, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            cancellationToken.ThrowIfCancellationRequested();
            var scope = ResolveScope(rootPath);
            var projectFiles = scope.ProjectPaths.ToHashSet(PathComparer);
            var tsConfigs = new HashSet<string>(PathComparer);
            var allFiles = scope.ScanDirectories.Concat(new[] { scope.RootPath })
                .Distinct(PathComparer)
                .SelectMany(directory => EnumerateAllFiles(directory, AnalysisOptions.DefaultIgnorePatterns, cancellationToken))
                .Distinct(PathComparer)
                .ToArray();
            var behaviorSpecPaths = new List<string>();
            var configurationPaths = new List<string>();
            foreach (var file in allFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Path.GetFileName(file).StartsWith("tsconfig", StringComparison.OrdinalIgnoreCase) &&
                    Path.GetExtension(file).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    tsConfigs.Add(file);
                }

                if (Path.GetExtension(file).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                    projectFiles.Add(file);
                if (Path.GetExtension(file).Equals(".feature", StringComparison.OrdinalIgnoreCase))
                    behaviorSpecPaths.Add(file);
                if (RepositoryEvidenceScanner.IsConfigurationFile(file))
                    configurationPaths.Add(file);
            }

            if (scope.SolutionPath == null && projectFiles.Count == 0 && tsConfigs.Count == 0 &&
                configurationPaths.Count == 0 && behaviorSpecPaths.Count == 0)
                return null;

            var inventoryFiles = allFiles.Where(RepositoryEvidenceScanner.IsConfigurationFile).ToArray();
            var evidence = await RepositoryEvidenceScanner.ScanAsync(inventoryFiles, scope.RootPath, cancellationToken).ConfigureAwait(false);
            return new ProjectInfo(
                scope.SolutionPath,
                projectFiles.OrderBy(path => path, PathComparer).ToArray(),
                tsConfigs.OrderBy(path => path, PathComparer).ToArray())
            {
                Evidence = evidence,
                ConfigurationPaths = configurationPaths.OrderBy(path => path, PathComparer)
                    .Select(path => Path.GetRelativePath(scope.RootPath, path).Replace('\\', '/')).ToArray(),
                BehaviorSpecPaths = behaviorSpecPaths.OrderBy(path => path, PathComparer)
                    .Select(path => Path.GetRelativePath(scope.RootPath, path).Replace('\\', '/')).ToArray()
            };
        }

        public string GetLanguage(FileInfo file) => Path.GetExtension(file.Name).ToLowerInvariant() switch
        {
            ".cs" => "csharp",
            ".ts" or ".js" => "typescript",
            ".tsx" or ".jsx" => "typescript-react",
            ".html" => "html",
            _ => "unknown"
        };

        private static Scope ResolveScope(
            string rootPath,
            IReadOnlyList<string>? ignorePatterns = null)
        {
            ignorePatterns ??= AnalysisOptions.DefaultIgnorePatterns;
            var fullPath = Path.GetFullPath(rootPath);
            if (Directory.Exists(fullPath))
            {
                var projects = EnumerateAllFiles(fullPath, ignorePatterns, CancellationToken.None)
                    .Where(path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return new Scope(fullPath, FindSolution(fullPath), projects, new[] { fullPath });
            }

            if (!File.Exists(fullPath))
                throw new DirectoryNotFoundException($"Input path does not exist: {fullPath}");

            var extension = Path.GetExtension(fullPath);
            var rootDirectory = Path.GetDirectoryName(fullPath)!;
            if (extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                return new Scope(rootDirectory, null, new[] { fullPath }, new[] { rootDirectory });

            if (extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) || extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
            {
                var projects = extension.Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ? ReadSolutionProjects(fullPath)
                    : ReadSolutionXmlProjects(fullPath);
                var scanDirectories = projects
                    .Select(Path.GetDirectoryName)
                    .Where(directory => directory != null)
                    .Cast<string>()
                    .Distinct(PathComparer)
                    .ToArray();
                return new Scope(rootDirectory, fullPath, projects, scanDirectories);
            }

            throw new ArgumentException("Input must be a directory, .sln, .slnx, or .csproj file.", nameof(rootPath));
        }

        private static string? FindSolution(string rootPath) =>
            Directory.EnumerateFiles(rootPath, "*.sln", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, PathComparer)
                .FirstOrDefault();

        private static string[] ReadSolutionProjects(string solutionPath)
        {
            var rootDirectory = Path.GetDirectoryName(solutionPath)!;
            return File.ReadLines(solutionPath)
                .Select(line => line.Split(','))
                .Where(parts => parts.Length >= 2 && parts[0].Contains("Project(", StringComparison.Ordinal))
                .Select(parts => parts[1].Trim().Trim('"'))
                .Where(path => Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFullPath(Path.Combine(rootDirectory, path.Replace('\\', Path.DirectorySeparatorChar))))
                .Where(File.Exists)
                .Distinct(PathComparer)
                .ToArray();
        }

        private static string[] ReadSolutionXmlProjects(string solutionPath)
        {
            var rootDirectory = Path.GetDirectoryName(solutionPath)!;
            return XDocument.Load(solutionPath)
                .Descendants()
                .Select(element => (string?)element.Attribute("Path"))
                .Where(path => path != null && Path.GetExtension(path).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
                .Select(path => Path.GetFullPath(Path.Combine(rootDirectory, path!.Replace('\\', Path.DirectorySeparatorChar))))
                .Where(File.Exists)
                .Distinct(PathComparer)
                .ToArray();
        }

        private static IEnumerable<FileInfo> EnumerateFiles(
            string rootPath,
            IReadOnlyList<string> ignorePatterns,
            CancellationToken cancellationToken)
        {
            var files = new List<FileInfo>();
            ScanDirectory(new DirectoryInfo(rootPath), files, ignorePatterns, cancellationToken);
            return files.Where(file => SupportedExtensions.Contains(file.Extension));
        }

        private static IEnumerable<string> EnumerateAllFiles(
            string rootPath,
            IReadOnlyList<string> ignorePatterns,
            CancellationToken cancellationToken)
        {
            var files = new List<FileInfo>();
            ScanDirectory(new DirectoryInfo(rootPath), files, ignorePatterns, cancellationToken, sourceFilesOnly: false);
            return files.Select(file => file.FullName);
        }

        private static void ScanDirectory(
            DirectoryInfo directory,
            List<FileInfo> files,
            IReadOnlyList<string> ignorePatterns,
            CancellationToken cancellationToken,
            bool sourceFilesOnly = true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (IsIgnoredDirectory(directory.Name, ignorePatterns) ||
                    (directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    return;

                var directoryFiles = directory.EnumerateFiles();
                files.AddRange(sourceFilesOnly
                    ? directoryFiles.Where(file => SupportedExtensions.Contains(file.Extension))
                    : directoryFiles);
                foreach (var subDirectory in directory.EnumerateDirectories())
                    ScanDirectory(subDirectory, files, ignorePatterns, cancellationToken, sourceFilesOnly);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (DirectoryNotFoundException)
            {
            }
        }

        private static bool IsTestFile(FileInfo file, string rootPath)
        {
            var directory = file.Directory;
            while (directory != null && IsWithinDirectory(directory.FullName, rootPath))
            {
                if (directory.Name.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                    directory.Name.Equals("tests", StringComparison.OrdinalIgnoreCase))
                    return true;
                directory = directory.Parent;
            }

            var name = Path.GetFileNameWithoutExtension(file.Name);
            return name.EndsWith("Tests", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Test", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Test", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsIgnoredDirectory(string name, IReadOnlyList<string> patterns) =>
            patterns.Any(pattern => FileSystemName.MatchesSimpleExpression(
                Path.GetFileName(pattern.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), name, true));

        private static bool IsWithinDirectory(string filePath, string directoryPath)
        {
            var relative = Path.GetRelativePath(directoryPath, filePath);
            return !Path.IsPathRooted(relative) && relative != ".." &&
                !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
        }

        private static StringComparer PathComparer => OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private sealed record Scope(string RootPath, string? SolutionPath, string[] ProjectPaths, string[] ScanDirectories);
    }
}
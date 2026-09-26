using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CodeExplainer.Scanner
{
    /// <summary>
    /// Scans a directory for source code files.
    /// </summary>
    public class FileScanner
    {
        private static readonly string[] CSharpExtensions = { ".cs" };
        private static readonly string[] TypeScriptExtensions = { ".ts", ".tsx" };
        private static readonly string[] HtmlExtensions = { ".html" };
        private static readonly string[] IgnorePatterns = { "node_modules", "bin", "obj", ".git", "dist", "build" };

        /// <summary>
        /// Scans a directory for source files.
        /// </summary>
        public async Task<IEnumerable<FileInfo>> ScanAsync(DirectoryInfo rootDirectory)
        {
            var files = new List<FileInfo>();

            await Task.Run(() =>
            {
                ScanDirectoryRecursive(rootDirectory, files);
            });

            return files;
        }

        private void ScanDirectoryRecursive(DirectoryInfo directory, List<FileInfo> files)
        {
            try
            {
                // Skip ignored directories
                if (IgnorePatterns.Any(pattern => directory.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                    return;

                // Add source files
                var sourceFiles = directory.GetFiles()
                    .Where(f => IsSupportedFile(f))
                    .ToList();

                files.AddRange(sourceFiles);

                // Recurse into subdirectories
                foreach (var subDir in directory.GetDirectories())
                {
                    ScanDirectoryRecursive(subDir, files);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        private bool IsSupportedFile(FileInfo file)
        {
            var extension = file.Extension.ToLowerInvariant();
            return CSharpExtensions.Contains(extension) ||
                   TypeScriptExtensions.Contains(extension) ||
                   HtmlExtensions.Contains(extension);
        }

        public string GetLanguage(FileInfo file)
        {
            var extension = file.Extension.ToLowerInvariant();
            return extension switch
            {
                ".cs" => "csharp",
                ".ts" => "typescript",
                ".tsx" => "typescript-react",
                ".html" => "html",
                _ => "unknown"
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core.Abstractions;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Analyzers.TypeScript
{
    /// <summary>Analyzes TypeScript code by communicating with the Node.js AST worker.</summary>
    public sealed class TypeScriptAnalyzer
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly string _workerPath;
        private readonly int _timeoutMs;

        public TypeScriptAnalyzer(string workerPath, int timeoutMs = 30000)
        {
            _workerPath = workerPath ?? string.Empty;
            _timeoutMs = timeoutMs > 0 ? timeoutMs : throw new ArgumentOutOfRangeException(nameof(timeoutMs));
        }

        public async Task<IEnumerable<CodeSymbol>> AnalyzeAsync(
            string rootPath,
            IEnumerable<string> files,
            IEnumerable<string> frameworks,
            CancellationToken cancellationToken = default) =>
            (await AnalyzeResultAsync(rootPath, files, frameworks, cancellationToken).ConfigureAwait(false)).Symbols;

        public async Task<AnalyzerResult> AnalyzeResultAsync(
            string rootPath,
            IEnumerable<string> files,
            IEnumerable<string> frameworks,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
            ArgumentNullException.ThrowIfNull(files);
            ArgumentNullException.ThrowIfNull(frameworks);
            var resolvedRoot = Path.GetFullPath(rootPath);
            var workerScript = ResolveWorkerScript(_workerPath);
            if (!File.Exists(workerScript))
            {
                throw new FileNotFoundException("TypeScript analyzer worker was not found. Build workers/typescript-analyzer with npm install and npm run build, or configure WorkerPath.", workerScript);
            }

            var request = new WorkerRequest
            {
                RootPath = resolvedRoot,
                Files = files.Select(file => Path.GetRelativePath(resolvedRoot, Path.GetFullPath(file, resolvedRoot))).ToArray(),
                Frameworks = frameworks.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
            var startInfo = new ProcessStartInfo(Environment.GetEnvironmentVariable("CODEEXPLAINER_NODE_PATH") ?? "node")
            {
                WorkingDirectory = Path.GetDirectoryName(workerScript)!,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(workerScript);

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            if (!process.Start()) throw new InvalidOperationException("Failed to start the TypeScript analyzer worker process.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_timeoutMs);
            var stderrTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try
            {
                await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request, JsonOptions).AsMemory(), timeout.Token).ConfigureAwait(false);
                process.StandardInput.Close();
                var responseLine = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(responseLine))
                    throw new InvalidOperationException($"TypeScript analyzer worker returned no response (exit code {process.ExitCode}). {stderr}");
                var response = JsonSerializer.Deserialize<WorkerResponse>(responseLine, JsonOptions)
                    ?? throw new InvalidOperationException("TypeScript analyzer worker returned invalid JSON.");
                if (!response.Success)
                    throw new InvalidOperationException($"TypeScript analyzer worker failed: {response.Error ?? stderr}");

                return new AnalyzerResult(
                    response.Symbols,
                    response.Relationships,
                    response.Diagnostics);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                if (cancellationToken.IsCancellationRequested) throw;
                throw new TimeoutException($"TypeScript analyzer worker exceeded the {_timeoutMs} ms timeout.");
            }
        }

        private static string ResolveWorkerScript(string configuredPath)
        {
            var candidate = configuredPath;
            if (string.IsNullOrWhiteSpace(candidate))
                candidate = Environment.GetEnvironmentVariable("CODEEXPLAINER_TYPESCRIPT_WORKER") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                var fullPath = Path.GetFullPath(candidate);
                if (Directory.Exists(fullPath)) return Path.Combine(fullPath, "dist", "index.js");
                if (Path.GetFileName(fullPath).Equals("package.json", StringComparison.OrdinalIgnoreCase))
                    return Path.Combine(Path.GetDirectoryName(fullPath)!, "dist", "index.js");
                return fullPath;
            }

            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var script = Path.Combine(directory.FullName, "workers", "typescript-analyzer", "dist", "index.js");
                if (File.Exists(script)) return script;
                directory = directory.Parent;
            }
            return Path.Combine(AppContext.BaseDirectory, "workers", "typescript-analyzer", "dist", "index.js");
        }

        private sealed class WorkerRequest
        {
            public string RootPath { get; init; } = string.Empty;
            public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
            public IReadOnlyList<string> Frameworks { get; init; } = Array.Empty<string>();
        }

        private sealed class WorkerResponse
        {
            public bool Success { get; init; }
            public List<CodeSymbol> Symbols { get; init; } = new();
            public List<CodeRelationship> Relationships { get; init; } = new();
            public List<AnalysisDiagnostic> Diagnostics { get; init; } = new();
            public string? Error { get; init; }
        }
    }
}
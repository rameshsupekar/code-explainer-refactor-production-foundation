using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeExplainer.Core.Models;

namespace CodeExplainer.Core.Abstractions
{
    /// <summary>
    /// Discovers source files and project structure.
    /// </summary>
    public interface ISourceDiscovery
    {
        /// <summary>
        /// Discovers all supported source files in the given root path.
        /// </summary>
        Task<IEnumerable<CodeFile>> DiscoverFilesAsync(
            string rootPath,
            AnalysisOptions options,
            CancellationToken cancellationToken);

        /// <summary>
        /// Discovers project metadata (solution, projects, tsconfig files).
        /// </summary>
        Task<ProjectInfo?> DiscoverProjectAsync(
            string rootPath,
            CancellationToken cancellationToken);
    }
}

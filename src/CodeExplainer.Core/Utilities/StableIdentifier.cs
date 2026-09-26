using System;
using System.Security.Cryptography;
using System.Text;

namespace CodeExplainer.Core.Utilities
{
    /// <summary>
    /// Generates stable, deterministic identifiers for symbols and diagram nodes.
    /// </summary>
    public static class StableIdentifier
    {
        /// <summary>
        /// Creates a stable symbol ID from language, file path, and symbol name.
        /// </summary>
        public static string CreateSymbolId(string language, string filePath, string symbolName)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException(nameof(language));
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrEmpty(symbolName)) throw new ArgumentNullException(nameof(symbolName));

            var normalized = $"{language}:{NormalizePath(filePath)}:{symbolName}";
            return normalized;
        }

        /// <summary>
        /// Creates a stable symbol ID for fully-qualified C# symbols.
        /// </summary>
        public static string CreateCSharpSymbolId(string filePath, string fullyQualifiedName)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrEmpty(fullyQualifiedName)) throw new ArgumentNullException(nameof(fullyQualifiedName));

            return CreateSymbolId("csharp", filePath, fullyQualifiedName);
        }

        /// <summary>
        /// Creates a stable symbol ID for TypeScript symbols.
        /// </summary>
        public static string CreateTypeScriptSymbolId(string filePath, string exportedName)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (string.IsNullOrEmpty(exportedName)) throw new ArgumentNullException(nameof(exportedName));

            return CreateSymbolId("typescript", filePath, exportedName);
        }

        /// <summary>
        /// Creates a stable node ID for diagram rendering (short, URL-safe).
        /// Uses SHA256 first 12 chars prefixed with 'N' for 'Node'.
        /// </summary>
        public static string CreateDiagramNodeId(string symbolId)
        {
            if (string.IsNullOrEmpty(symbolId)) throw new ArgumentNullException(nameof(symbolId));

            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(symbolId));
            return "N" + Convert.ToHexString(bytes)[..12].ToLowerInvariant();
        }

        /// <summary>
        /// Normalizes file paths to forward slashes for consistent ID generation.
        /// </summary>
        private static string NormalizePath(string filePath)
        {
            return filePath.Replace('\\', '/').TrimStart('/');
        }

        /// <summary>
        /// Escapes special characters in labels for Mermaid diagrams.
        /// </summary>
        public static string EscapeMermaidLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;

            return label
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;")
                .Replace("[", "&#91;")
                .Replace("]", "&#93;")
                .Replace("(", "&#40;")
                .Replace(")", "&#41;")
                .Replace("{{", "&#123;&#123;")
                .Replace("}}", "&#125;&#125;")
                .Replace("\n", " ")
                .Replace("\r", " ");
        }

        /// <summary>
        /// Escapes special characters in labels for PlantUML diagrams.
        /// </summary>
        public static string EscapePlantUmlLabel(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;

            return label
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace(":", "\\:")
                .Replace("\n", "\\n")
                .Replace("\r", " ");
        }
    }
}

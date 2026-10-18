using System.Collections.Generic;

namespace CodeExplainer.Core.Models
{
    /// <summary>
    /// Represents the complete analyzed code model including diagnostics.
    /// </summary>
    public sealed record CodeModel(
        IReadOnlyList<CodeFile> Files,
        IReadOnlyList<CodeSymbol> Symbols,
        IReadOnlyList<CodeRelationship> Relationships,
        IReadOnlyList<AnalysisDiagnostic> Diagnostics);

    /// <summary>
    /// Represents a source code file.
    /// </summary>
    public sealed record CodeFile(
        string FilePath,
        string RelativePath,
        string Language,
        string? ProjectPath,
        int LineCount);

    /// <summary>
    /// Represents project context (solution/project files, TypeScript configs).
    /// </summary>
    public sealed record ProjectInfo(
        string? SolutionPath,
        IReadOnlyList<string> ProjectPaths,
        IReadOnlyList<string> TypeScriptConfigPaths)
    {
        public IReadOnlyList<RepositoryEvidence> Evidence { get; init; } = System.Array.Empty<RepositoryEvidence>();
        public IReadOnlyList<BehaviorFeature> BehaviorFeatures { get; init; } = System.Array.Empty<BehaviorFeature>();
        public IReadOnlyList<string> ConfigurationPaths { get; init; } = System.Array.Empty<string>();
        public IReadOnlyList<string> BehaviorSpecPaths { get; init; } = System.Array.Empty<string>();
    }

    /// <summary>
    /// A repository technology or infrastructure fact with its source and confidence.
    /// </summary>
    public sealed record RepositoryEvidence(
        string Name,
        string Category,
        string SourcePath,
        string EvidenceType,
        string Confidence,
        string? ProjectPath = null);

    /// <summary>
    /// A Gherkin feature found in the repository or generated as a reviewable draft.
    /// </summary>
    public sealed record BehaviorFeature(
        string Name,
        string SourcePath,
        IReadOnlyList<BehaviorScenario> Scenarios,
        bool IsGeneratedDraft = false);

    /// <summary>
    /// A scenario and its implementation links, when they can be established from evidence.
    /// </summary>
    public sealed record BehaviorScenario(
        string Name,
        IReadOnlyList<string> Tags,
        IReadOnlyList<string> Steps,
        IReadOnlyList<string> RelatedSymbolIds,
        bool IsGeneratedDraft = false);

    /// <summary>
    /// Represents a symbol in the code (class, interface, function, component, etc.).
    /// </summary>
    public sealed record CodeSymbol(
        string Id,
        string Name,
        SymbolKind Kind,
        string Language,
        string FilePath,
        SourceLocation Location,
        IReadOnlyList<CodeMember> Members,
        IReadOnlyDictionary<string, string> Metadata);

    /// <summary>
    /// Represents a member of a symbol (method, property, field).
    /// </summary>
    public sealed record CodeMember(
        string Id,
        string Name,
        MemberKind Kind,
        SourceLocation Location,
        IReadOnlyList<string> Parameters,
        string? ReturnType);

    /// <summary>
    /// Represents a relationship between two symbols.
    /// </summary>
    public sealed record CodeRelationship(
        string SourceId,
        string TargetId,
        RelationshipKind Kind,
        string? Label);

    /// <summary>
    /// Source code location information.
    /// </summary>
    public sealed record SourceLocation(
        int StartLine,
        int EndLine,
        int StartColumn,
        int EndColumn);

    /// <summary>
    /// Analysis diagnostic (warning, error, or info).
    /// </summary>
    public sealed record AnalysisDiagnostic(
        string Code,
        DiagnosticSeverity Severity,
        string Message,
        string? FilePath = null,
        SourceLocation? Location = null);

    /// <summary>
    /// Severity levels for analysis diagnostics.
    /// </summary>
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Kinds of symbols.
    /// </summary>
    public enum SymbolKind
    {
        Namespace,
        Module,
        Class,
        Interface,
        Method,
        Function,
        Component,           // Angular/React component
        Service,             // Angular service
        Directive,           // Angular directive
        Pipe,                // Angular pipe
        Controller,          // ASP.NET Core controller
        Endpoint,            // API endpoint
        Hook,                // React hook
        ContextProvider,     // React Context provider
        Type,                // TypeScript type alias
        Enum,                // Enum definition
        Decorator            // Decorator
    }

    /// <summary>
    /// Kinds of members.
    /// </summary>
    public enum MemberKind
    {
        Method,
        Property,
        Field,
        Constructor,
        Getter,
        Setter,
        Parameter
    }

    /// <summary>
    /// Kinds of relationships between symbols.
    /// </summary>
    public enum RelationshipKind
    {
        Contains,            // A namespace/module contains a symbol
        Imports,             // Imports/uses another symbol
        Inherits,            // Inherits from another class
        Implements,          // Implements an interface
        Calls,               // Calls a method/function
        Injects,             // Dependency injection
        Uses,                // Uses a service or utility
        Renders,             // React component renders another component
        RoutesTo,            // Angular/React routing
        Reads,               // Reads a property/field
        Writes,              // Writes to a property/field
        Decorates,           // Angular decorator
        Provides,            // Angular provider
        References           // General reference
    }
}

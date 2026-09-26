# CodeExplainer

A cross-language source code architecture analyzer for .NET, C#, TypeScript, Angular, and React. Generates Mermaid or PlantUML diagrams explaining code structure and dependencies.

## Overview

CodeExplainer analyzes multi-language codebases and generates visual architecture diagrams:

- **C# Analysis**: Uses Roslyn for structural analysis via syntax trees and semantic models
- **TypeScript/Angular/React**: Node.js worker using TypeScript Compiler API
- **Common Model**: Unified intermediate representation across all languages
- **Diagram Export**: Mermaid (flowcharts) and PlantUML (class, component, sequence)

## Architecture

```
Source Code
    ↓
Language-specific Analyzer
    ↓
Common Intermediate Representation (CodeModel)
    ↓
Diagram Generator (Mermaid / PlantUML)
```

### Project Structure

```
src/
├── CodeExplainer.Cli/                 # CLI entry point
├── CodeExplainer.Core/                # Core models and interfaces
├── CodeExplainer.Scanner/             # File scanning and detection
├── CodeExplainer.Analyzers.CSharp/    # Roslyn-based C# analyzer
├── CodeExplainer.Analyzers.TypeScript/ # TypeScript compiler wrapper
├── CodeExplainer.Analyzers.Angular/   # Angular-specific extraction
├── CodeExplainer.Analyzers.React/     # React-specific extraction
├── CodeExplainer.Relationships/       # Cross-language relationship builder
├── CodeExplainer.Export.Mermaid/      # Mermaid diagram generation
├── CodeExplainer.Export.PlantUml/     # PlantUML diagram generation
└── CodeExplainer.Export.Markdown/     # Markdown documentation export

workers/
└── typescript-analyzer/               # Node.js TypeScript analyzer worker
```

## Core Models

### CodeSymbol
Represents a symbol in the code (class, interface, function, component, etc.)

### SymbolKind
- Namespace, Module, Class, Interface, Method, Function
- Component, Service, Directive, Pipe, Module (Angular)
- Controller, Endpoint, Hook, ContextProvider, Type, Enum (TypeScript/React)

### CodeRelationship
Connects symbols with typed relationships:
- Contains, Imports, Inherits, Implements, Calls
- Injects, Uses, Renders, RoutesTo, Reads, Writes
- Decorates, Provides, References

## Usage

### Basic Analysis

```bash
code-explainer analyze ./repository \
  --diagram dependency,class \
  --format mermaid \
  --output ./docs/generated
```

### Output Files
- `analysis.json` - Complete code model
- `dependencies.mmd` - Dependency diagram
- `classes.mmd` - Class diagram
- `summary.md` - Markdown documentation

## Development Milestones

### MVP (Phase 1)
- [x] Solution structure and project setup
- [ ] CLI with command parsing
- [ ] File scanner for C#, TypeScript, Angular, React, HTML
- [ ] Common intermediate model
- [ ] Dependency diagram generation (Mermaid)
- [ ] Class diagram generation (Mermaid)

### Phase 2: C# Analysis
- [ ] Roslyn solution/project loading
- [ ] Extract namespaces, classes, interfaces
- [ ] Resolve inheritance and implementations
- [ ] ASP.NET Core controller and endpoint detection
- [ ] Dependency injection tracking

### Phase 3: TypeScript Worker
- [ ] TypeScript Program creation from tsconfig
- [ ] Module, class, interface, and function extraction
- [ ] Import/export resolution
- [ ] Worker communication via stdin/stdout

### Phase 4: Framework Support
- [ ] Angular decorators (@Component, @Injectable, @Directive, @Pipe)
- [ ] Angular routing and dependency injection
- [ ] React components and hooks (useState, useEffect, useContext)
- [ ] JSX component references
- [ ] React Router integration

### Phase 5: Advanced Diagrams
- [ ] Method call graphs
- [ ] Sequence diagram approximations
- [ ] Controller → Service → Repository flows
- [ ] Circular dependency detection
- [ ] Large diagram splitting

### Phase 6: Reliability
- [ ] Caching and incremental analysis
- [ ] Cancellation token support
- [ ] Parallel file analysis
- [ ] Comprehensive error diagnostics
- [ ] Snapshot tests for diagrams
- [ ] Integration tests with sample projects

## Technologies

### C# / .NET
- **Microsoft.CodeAnalysis.CSharp.Workspaces** - Roslyn syntax trees and semantic models
- **Microsoft.Build.Locator** - MSBuild workspace resolution
- **System.CommandLine** - CLI argument parsing

### TypeScript / Node.js
- **typescript** - TypeScript Compiler API
- **@angular/compiler** - Angular template compilation
- **@angular/compiler-cli** - Angular compiler utilities

### Export
- **Mermaid** - Lightweight diagram syntax
- **PlantUML** - Comprehensive UML diagram support

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- Node.js 18+ (for TypeScript worker)

### Build

```bash
dotnet build CodeExplainer.sln
cd workers/typescript-analyzer && npm install && npm run build
```

### Run

```bash
cd src/CodeExplainer.Cli
dotnet run -- analyze ./path/to/code --format mermaid --output ./output
```

## License

MIT

# 03.02-csharp-workflows: Extract C# UI, API, service, and worker workflow evidence

## Objective
Expand C# analysis so folder-based reverse engineering discovers user-facing endpoints and backend execution components instead of only classes and members.

## Scope
Update `src/CodeExplainer.Analyzers.CSharp/CSharpAnalyzer.cs` and related model/analysis interfaces only as needed. Use Roslyn syntax/semantic evidence for ASP.NET controllers and route attributes, minimal API route mappings, service dependencies/calls, and hosted/background workers. Include Razor/Blazor entry points through scanner integration only if required by the agreed file model; keep this task focused on C# symbol and relationship evidence.

## Steps
1. Add fixture-driven tests for controller routes, minimal API endpoints, service dependencies, and hosted worker types.
2. Extract stable symbol IDs, source locations, route/method metadata, and evidence-backed relationships.
3. Keep unresolved or unsupported constructs diagnosable; do not infer unrelated names as links.
4. Run the focused tests and build the analyzer and its dependents.

**Done when**: Tests show that C# route, service, and background-worker elements become stable model symbols/relationships with correct source locations, and the affected projects build warning-free.

## Research Findings

- The analyzer is a small syntax-only project (`src/CodeExplainer.Analyzers.CSharp`) with Roslyn Workspaces 4.8.0, referencing Core. `CSharpAnalyzer.AnalyzeAsync(filePath, content, cancellationToken)` currently returns `IEnumerable<CodeSymbol>`, not the common `ICodeAnalyzer` result, so this task extends existing symbol extraction rather than changing orchestration contracts.
- `CodeSymbol` supports Controller, Endpoint and Service kinds and string metadata; `StableIdentifier.CreateCSharpSymbolId` provides path-aware IDs. The current CodeModel has no explicit endpoint verb/route fields, so metadata keys `httpMethod`, `route`, `controllerId`, and `action` carry those facts.
- Implemented syntax-evidence extraction for MVC-style controller classes, HTTP verb attributes and route templates, minimal API `MapGet/MapPost/MapPut/MapDelete/MapPatch` calls with literal paths, constructor parameter dependency names, conventional service/repository type names, and `BackgroundService`/`IHostedService` base types.
- Test coverage resides in `tests/CodeExplainer.Tests/CSharpAnalyzerTests.cs`; the shared project now references the C# analyzer. Tests cover route composition/source location, constructor dependency metadata, minimal API route extraction, and background worker classification.
- Limitation: no semantic compilation is available here, so service references/DI resolutions aren't linked yet; task 03.04 will resolve relationships across metadata. Dynamic/computed routes and unsupported routing patterns are not currently extracted. Avoid claiming completeness beyond syntax patterns covered by fixtures.

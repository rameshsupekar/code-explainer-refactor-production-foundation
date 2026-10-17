# 03.03-typescript-workflows: Connect the TypeScript worker and extract React and Angular workflows

## Objective
Make the existing TypeScript AST worker usable from .NET and enrich it to expose application entry points, routes, frontend-to-service/API requests, and background workers for React and Angular projects.

## Scope
Update `src/CodeExplainer.Analyzers.TypeScript/TypeScriptAnalyzer.cs`, the React/Angular wrappers, and `workers/typescript-analyzer/src/{index.ts,models.ts}` plus worker build configuration as needed. The worker currently emits partial modules/types/functions but lacks route, DI, component-render, HTTP, job, and diagnostics coverage; the .NET adapter currently returns an empty result without starting Node.

## Steps
1. Add TypeScript fixtures for React components/routes/hooks/API calls and Angular components/routes/services/DI/templates.
2. Implement the .NET-to-Node JSON-lines process contract with timeout, cancellation, stderr/error handling, and portable worker resolution.
3. Extend AST extraction with stable relative paths, source locations, explicit route/render/injection/API-call/job metadata, and diagnostics for unsupported patterns.
4. Add built-in Node tests or equivalent automated worker tests and build the worker.
5. Run focused .NET integration tests against the worker and build affected projects.

**Done when**: React and Angular fixtures produce the expected symbols and relationships through the actual .NET adapter, worker errors/timeouts are surfaced as diagnostics, and tests plus builds pass without warnings.

## Research Findings

- The worker contract is one JSON request per input line and one JSON response per output line. DTO field names match `System.Text.Json` defaults only if casing options are configured; C# records use PascalCase, so enable case-insensitive parsing and camelCase serialization explicitly.
- `TypeScriptAnalyzer` currently serializes only root/files/frameworks and returns an empty list. It has no cancellation token, diagnostics return type, relationships, process launch, or timeout handling. Keep its existing method for React/Angular wrappers and add a richer `AnalyzeResultAsync` method for full results while preserving compatibility.
- Worker discovery must work from a configured `AnalysisOptions.WorkerPath`, an environment override, and repository-relative development paths. Worker requires `npm install` before `npm run build`; Node/npm exist but no dependencies or lockfile are currently present.
- Worker currently bases module paths on `process.cwd()` and emits absolute paths in symbols. Change IDs and paths to root-relative slash-normalized values, parse tsconfig options without overriding them with defaults, detect React/Angular using supplied frameworks and syntax evidence, and preserve source locations.
- C# consumers can add tests through `tests/CodeExplainer.Tests`; add a reference to the TypeScript analyzer. Test the process bridge with a real built worker and a fixture rather than only DTO mapping, and report missing Node/worker failures as actionable exceptions for the orchestration layer to turn into diagnostics.
- AST coverage target for this subtask: modules/imports, functions/classes, React components and JSX render edges, React Router route declarations where recognizable, Angular components/services/constructor injection and route arrays, literal fetch/axios/HttpClient calls, and common worker/job symbol conventions. Dynamic forms remain diagnostic/unsupported, not guessed.

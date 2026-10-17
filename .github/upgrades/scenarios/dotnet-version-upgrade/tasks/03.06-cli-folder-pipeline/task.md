# 03.06-cli-folder-pipeline: Orchestrate folder analysis and write requested diagrams from the CLI

## Objective
Connect the CLI's `analyze <path>` command to discovery, analyzers, relationship resolution, explanations, and exporters so arbitrary folder inputs produce actual artifacts.

## Scope
Update `src/CodeExplainer.Cli/Program.cs`, `src/CodeExplainer.Scanner/FileScanner.cs`, and add a source-discovery/orchestration service in an appropriate existing project. Honor `AnalysisOptions`, ignore patterns, output format/type/path, and Node worker configuration. Discover solution/project and TypeScript config context when present; prevent output artifacts from being re-scanned. Keep all behavior generic to the provided root folder.

## Steps
1. Add CLI/integration tests that run against synthetic mixed C#/.NET and React/Angular fixture folders.
2. Implement deterministic folder discovery and project metadata collection with cancellation and clear diagnostics.
3. Invoke the C# and TypeScript analyzers, merge symbols, resolve relationships, and dispatch the requested exporters.
4. Write diagrams and structured explanations only under the requested output directory, handling invalid/unreadable input and unknown formats cleanly.
5. Run the end-to-end test suite and full solution build.

**Done when**: The CLI's analyze command produces requested output files from a mixed full-stack fixture, includes connected UI-to-backend/worker workflows when evidence is present, handles unsupported/missing constructs explicitly, and tests/builds pass without warnings.

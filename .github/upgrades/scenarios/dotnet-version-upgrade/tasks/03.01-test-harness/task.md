# 03.01-test-harness: Add automated tests for analysis and diagram behavior

## Objective
Establish an automated .NET 10 test project before expanding analyzer and diagram behavior.

## Research Findings

- **Solution and frameworks**: `CodeExplainer.sln` is an SDK-style solution with 11 production projects, all targeting `net10.0`; the Mermaid exporter references Core, and Core enables nullable analysis and treats warnings as errors. `dotnet build` is the selected build tool for this solution.
- **Test project absence**: Repository search found no existing test project, test source, test SDK, or worker test runner. The worker also has no `node_modules` or lockfile installed at present.
- **Central packages**: Root `Directory.Packages.props` enables CPM and centrally defines Locator 1.11.2, Roslyn Workspaces 4.8.0, and System.CommandLine 2.0.0. Add xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.11.0, and xunit.runner.visualstudio 4.0.1 here, with package references in the test project that do not specify versions.
- **Baseline coverage**: `StableIdentifier.CreateDiagramNodeId` is SHA-256-derived and deterministic. A Mermaid model/export test can exercise the public `MermaidExporter.ExportDependencyDiagram(CodeModel)` API. Later subtasks should add analyzer, relationship, worker, and CLI fixture tests to this same harness.
- **Scope chosen**: Create one `tests/CodeExplainer.Tests` net10.0 project with references to Core, Mermaid, and (for incremental subsequent coverage) analyzer/relationships/exporter projects as needed. Add it to the solution, then verify test discovery and a warning-free full solution build. Do not create any README files.

## Scope
Create a test project under `tests/`, add it to `CodeExplainer.sln`, and register test package versions centrally in the existing `Directory.Packages.props`. Use the resolved stable versions already researched: xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.11.0, and xunit.runner.visualstudio 4.0.1. Do not create README files.

## Steps
1. Create the test project targeting net10.0 and add the required production-project references.
2. Centralize its test dependencies and remove per-project package versions.
3. Add baseline tests for deterministic identifiers and a minimal model/export path.
4. Build and run the new test project.

**Done when**: The solution contains a discoverable test project; its tests run successfully; all package versions remain centrally managed; no README files are created.

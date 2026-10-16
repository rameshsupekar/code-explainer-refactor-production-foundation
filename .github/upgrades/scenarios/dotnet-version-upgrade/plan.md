# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade every applicable project in CodeExplainer.sln to .NET 10 and update the requested Build Locator dependency while retaining the analyzer's general-purpose behavior. Review and strengthen folder-wide application workflow diagrams for supplied C#/.NET and TypeScript React/Angular projects.
**Scope**: 11 SDK-style projects currently targeting net8.0, a CLI compatibility cluster reported in `src/CodeExplainer.Cli/Program.cs`, centralized package management, and the scanner/analyzer/exporter/CLI path that produces diagrams for arbitrary input folders.

## Tasks

### Selected Strategy
**All-at-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 11 SDK-style projects target net8.0, the project graph is straightforward, packages are reported compatible, and the upgrade is modern-to-modern.

### 01-package-management: Centralize package versions and verify upgrade prerequisites

Configure Central Package Management across the SDK-style solution and verify the .NET 10 toolchain and repository SDK configuration before changing target frameworks. The assessment found 11 projects and no existing `Directory.Packages.props`; the user confirmed CPM. Research package declarations and imports so centralization preserves project-specific dependency intent and the current solution can still restore.

**Done when**: Package versions are centrally defined without duplicate project-level versions, the .NET 10 SDK/configuration is verified, and restore succeeds for the existing solution.

---

### 02-dotnet10-upgrade: Upgrade all projects and resolve CLI compatibility

Upgrade all 11 projects together from net8.0 to net10.0 and keep package versions compatible with the target. Update `Microsoft.Build.Locator` to the latest stable supported version resolved for net10.0 (1.11.2 at planning time). Resolve the assessment's 13 `System.CommandLine` source-compatibility findings in `src/CodeExplainer.Cli/Program.cs`; validate the actual package API rather than assuming the findings are framework API changes.

**Done when**: Every applicable project targets net10.0, the CLI uses compatible command-line APIs, package restore succeeds, and the full solution builds without warnings or errors.

---

### 03-application-workflow-diagrams: Review and improve folder-wide business workflow diagrams

Trace the supplied folder through scanning, language/framework detection, analyzers, relationship extraction, diagram construction, and export. Ensure generated diagrams explain discoverable application use cases and user journeys across UI/frontend entry points, backend APIs, services, and background workers where those relationships can be inferred from the input. Keep behavior generic across C#/.NET and TypeScript React/Angular projects; represent uncertainty or unsupported constructs honestly rather than hard-coding a validation application's names or paths.

**Done when**: Tests demonstrate that a folder input can produce useful, connected workflow diagrams from discovered application components and interactions; supported project types and gaps are explicit, and no behavior depends on a fixed input path.

---

### 04-validation: Validate builds, tests, and real-folder diagram generation

Run the complete solution build and available automated tests after the framework and diagram changes. Exercise CodeExplainer against this repository and, if accessible, `C:\Work\UnitsConverter`; inspect generated explanations and diagrams for discovered user workflows and cross-component paths. Treat UnitsConverter only as a validation input and report clearly if it is unavailable.

**Done when**: The solution builds warning-free, available tests pass, diagram generation is exercised for the repository and any accessible external validation folder, and results or access limitations are documented.

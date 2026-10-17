# 02-dotnet10-upgrade: Upgrade all projects and resolve CLI compatibility

Upgrade all 11 projects together from net8.0 to net10.0 and keep package versions compatible with the target. Update `Microsoft.Build.Locator` to the latest stable supported version resolved for net10.0 (1.11.2 at planning time). Resolve the assessment's 13 `System.CommandLine` source-compatibility findings in `src/CodeExplainer.Cli/Program.cs`; validate the actual package API rather than assuming the findings are framework API changes.

**Done when**: Every applicable project targets net10.0, the CLI uses compatible command-line APIs, package restore succeeds, and the full solution builds without warnings or errors.

## Scope Inventory

- **Projects affected**: All 11 solution projects were queried against the assessment. Actual source files and branch HEAD already target net10.0: CLI, Core, Scanner, CSharp/TypeScript/React/Angular analyzers, Relationships, Mermaid/PlantUML/Markdown exporters. No TFM change is needed.
- **Assessment/source mismatch**: The generated assessment still reports net8.0 for every project and 13 CLI source-incompatible issues. It was generated before the current branch state. The CLI assessment's reported APIs are from the `System.CommandLine` package (2.0.0), not .NET BCL APIs.
- **Current validation of reported APIs**: The CLI compiles successfully against `System.CommandLine` 2.0.0 on the actual net10.0 target. Its `Command`, `RootCommand`, `Argument`, `Option`, `Description`, `Arguments`, `Options`, `Subcommands`, and parse/invoke APIs are currently used in `src/CodeExplainer.Cli/Program.cs`; the .NET 10 full-solution build had 0 errors. Treat the analyzer findings as stale/false positives unless runtime validation finds a defect; do not rewrite working APIs based only on the report.
- **Package action**: `Microsoft.Build.Locator` is centrally managed in root `Directory.Packages.props`, currently 1.7.8. `get_supported_package_version` resolves latest stable net10.0-supported version 1.11.2. Search found no direct `MSBuildLocator` API usage in source; it is a direct dependency of `CodeExplainer.Analyzers.CSharp` and must be updated centrally as requested.
- **Other direct dependencies**: Roslyn Workspaces 4.8.0 and System.CommandLine 2.0.0 are centralized and reported compatible. No other package updates were recommended by assessment or requested.
- **Validation plan**: Restore and build the full solution after the Locator bump, inspect warning output, and invoke the CLI help path to verify its command parser at runtime. The solution has no discovered test projects.
- **Skill matches**: Target-framework management confirms no edits are needed because net10.0 is already present; package management requires updating the central PackageVersion; building-projects prescribes `dotnet` build/restore for this SDK-style solution. One package update plus validation is atomic; no decomposition needed.

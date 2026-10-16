# 02-dotnet10-upgrade: Upgrade all projects and resolve CLI compatibility

Upgrade all 11 projects together from net8.0 to net10.0 and keep package versions compatible with the target. Update `Microsoft.Build.Locator` to the latest stable supported version resolved for net10.0 (1.11.2 at planning time). Resolve the assessment's 13 `System.CommandLine` source-compatibility findings in `src/CodeExplainer.Cli/Program.cs`; validate the actual package API rather than assuming the findings are framework API changes.

**Done when**: Every applicable project targets net10.0, the CLI uses compatible command-line APIs, package restore succeeds, and the full solution builds without warnings or errors.

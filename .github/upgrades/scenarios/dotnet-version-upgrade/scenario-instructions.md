# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: Single Commit at End
- **Pace**: Standard
- **Target Framework**: net10.0
- **Build Locator**: Use the latest version
- **Diagram Scope**: Validate and improve folder-wide diagram generation and UI workflows without hard-coding behavior for `C:\Work\UnitsConverter`.
- **Validation Inputs**: Test reverse-engineering explanations/diagrams for this repository and `C:\Work\UnitsConverter` when accessible.
- **Documentation**: Do not create any README/readme files unless explicitly requested.

## Strategy
**Selected**: All-at-Once
**Rationale**: All 11 SDK-style projects already target net10.0 on the checked-out branch; the generated assessment still lists net8.0. All-at-Once remains appropriate for validating and completing the requested package/API work together.

### Execution Constraints
- Upgrade all project target frameworks together in one atomic operation; do not leave mixed target frameworks.
- Configure Central Package Management for the modern-to-modern solution and keep package versions centrally defined.
- Validate the full solution build after the atomic .NET upgrade and run tests after the upgrade succeeds.
- Resolve the System.CommandLine API findings in the CLI as part of the same upgrade task; do not defer compatibility work.
- Preserve general-purpose analysis across C#/.NET and TypeScript React/Angular, and test folder-wide business workflows through diagrams without product-specific path assumptions.

## Decisions
- Use working branch `upgrade-dotnet-10` based on `master`; branch sync is Auto (Merge).
- Treat this as a general-purpose reverse-engineering tool for C#/.NET and TypeScript React/Angular projects; UnitsConverter is a validation input, not a product-specific target.
- **Upgrade Strategy**: All-at-Once, confirmed by the user.
- **Package Management**: Central Package Management, confirmed by the user.
- **Diagram semantics**: Diagrams should explain the analyzed application's business use cases and user workflows, connecting UI, frontend, backend, services, and background workers discovered in the supplied folder. The analyzed folder is a general input; do not hard-code behavior for a validation repository.

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Package Management: Central Package Management (CPM)

## Build Tool Decisions
- **All CodeExplainer SDK-style projects**: `dotnet build` (the solution targets modern .NET and no WPF, embedded-resource, or legacy MSBuild requirements were found).

## User Preferences
### Technical Preferences
- **Project layout**: Do not introduce a new `src/` layout by moving only selected projects. If a `src/` layout is needed, move all existing production projects together and update solution/project references; otherwise preserve the current layout.

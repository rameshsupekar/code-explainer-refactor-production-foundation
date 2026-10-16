# .NET Version Upgrade

## Strategy
Upgrade all applicable solution projects to .NET 10 while keeping the reverse-engineering tool general-purpose across C#/.NET and TypeScript React/Angular inputs.

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: After Each Task
- **Pace**: Standard
- **Target Framework**: net10.0
- **Build Locator**: Use the latest version
- **Diagram Scope**: Validate and improve folder-wide diagram generation and UI workflows without hard-coding behavior for `C:\Work\UnitsConverter`.
- **Validation Inputs**: Test reverse-engineering explanations/diagrams for this repository and `C:\Work\UnitsConverter` when accessible.

## Decisions
- Use working branch `upgrade-dotnet-10` based on `master`; branch sync is Auto (Merge).
- Treat this as a general-purpose reverse-engineering tool for C#/.NET and TypeScript React/Angular projects; UnitsConverter is a validation input, not a product-specific target.
- **Upgrade Strategy**: All-at-Once, confirmed by the user.
- **Package Management**: Central Package Management, confirmed by the user.

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: All-at-Once

### Project Structure
- Package Management: Central Package Management (CPM)

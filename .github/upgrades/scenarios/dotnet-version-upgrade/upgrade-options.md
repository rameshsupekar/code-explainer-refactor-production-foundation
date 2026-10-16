# Upgrade Options — CodeExplainer

Assessment: 11 SDK-style projects targeting net8.0; upgrade to net10.0; package compatibility is reported as 100%; CLI source-compatibility findings involve System.CommandLine.

## Strategy

### Upgrade Strategy
All 11 projects are on net8.0, SDK-style, and the dependency graph is straightforward; an atomic upgrade avoids cross-target-framework incompatibilities while libraries and CLI are temporarily on different TFMs.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade all projects simultaneously in a single atomic pass. |
| Top-Down | Upgrade entry-point applications first, temporarily multi-targeting shared libraries so the solution stays buildable throughout. |

## Project Structure

### Package Management
The solution has 11 SDK-style projects, no existing Directory.Packages.props, and this is a modern-to-modern upgrade with no extended multi-targeting expected.

| Value | Description |
|-------|-------------|
| **Central Package Management (CPM)** (selected) | Create Directory.Packages.props and move package versions out of project files for consistent maintenance. |
| Per-Project (defer CPM to post-migration) | Keep package versions in project files during the migration and consider CPM afterward. |

# 01-package-management: Centralize package versions and verify upgrade prerequisites

Configure Central Package Management across the SDK-style solution and verify the .NET 10 toolchain and repository SDK configuration before changing target frameworks. The assessment found 11 projects and no existing `Directory.Packages.props`; the user confirmed CPM. Research package declarations and imports so centralization preserves project-specific dependency intent and the current solution can still restore.

**Done when**: Package versions are centrally defined without duplicate project-level versions, the .NET 10 SDK/configuration is verified, and restore succeeds for the existing solution.

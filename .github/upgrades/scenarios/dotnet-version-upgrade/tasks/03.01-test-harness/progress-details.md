# Progress details: 03.01-test-harness

## Changes
- Added `tests/CodeExplainer.Tests/CodeExplainer.Tests.csproj`, targeting `net10.0`, and registered it in `CodeExplainer.sln`.
- Added centrally managed xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.11.0, and xunit.runner.visualstudio 4.0.1 package versions in `Directory.Packages.props`; project references do not pin package versions.
- Added baseline tests in `tests/CodeExplainer.Tests/BaselineTests.cs` for stable diagram node IDs and Mermaid model/export behavior.
- No README/readme files were created.

## Validation
- `dotnet test tests/CodeExplainer.Tests/CodeExplainer.Tests.csproj` — passed; 2 tests succeeded, 0 failed.
- `dotnet build CodeExplainer.sln` — passed.
- The initial test attempt found missing explicit BCL/xUnit imports in the new test file; added the imports and reran successfully.

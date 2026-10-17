# 03.05-workflow-exporters: Export application workflows and explanations as Mermaid, PlantUML, and Markdown

## Objective
Provide usable diagrams and explanations for business workflows, not only dependency/class relationships.

## Scope
Implement the export paths in `MermaidExporter`, `PlantUmlExporter`, and `MarkdownExporter`. Support the CLI's advertised dependency, class, component, and sequence views plus a business-flow view when appropriate. Include UI routes, frontend components/services, APIs, backend services, and workers from model relationships. Use `StableIdentifier` for deterministic diagram node IDs and escape labels for each syntax.

## Steps
1. Add golden/assertion tests using cross-layer fixture models for each supported output type.
2. Implement component/workflow and sequence diagrams, and complete class/dependency/Markdown summaries.
3. Ensure empty input, unsupported symbol kinds, and ambiguous relationships render safely with clear diagnostics.
4. Build the exporters and run all focused tests.

**Done when**: Every advertised diagram type has a non-empty, deterministic export for a populated model; output contains the expected cross-component workflow edges and passes tests/build with no warnings.

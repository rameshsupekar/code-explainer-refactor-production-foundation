# 03-application-workflow-diagrams: Review and improve folder-wide business workflow diagrams

Trace the supplied folder through scanning, language/framework detection, analyzers, relationship extraction, diagram construction, and export. Ensure generated diagrams explain discoverable application use cases and user journeys across UI/frontend entry points, backend APIs, services, and background workers where those relationships can be inferred from the input. Keep behavior generic across C#/.NET and TypeScript React/Angular projects; represent uncertainty or unsupported constructs honestly rather than hard-coding a validation application's names or paths.

**Done when**: Tests demonstrate that a folder input can produce useful, connected workflow diagrams from discovered application components and interactions; supported project types and gaps are explicit, and no behavior depends on a fixed input path.

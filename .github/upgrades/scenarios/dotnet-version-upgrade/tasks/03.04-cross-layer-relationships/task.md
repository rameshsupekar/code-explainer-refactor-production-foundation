# 03.04-cross-layer-relationships: Resolve evidence-backed UI-to-backend and worker relationships

## Objective
Connect analyzer facts into application workflows across language and project boundaries without hard-coded repository paths or product-specific naming rules.

## Scope
Implement `IRelationshipResolver`/`src/CodeExplainer.Relationships/RelationshipBuilder.cs` and adjust `CodeModel` metadata only where necessary. Link UI routes/components to frontend services and HTTP requests, requests to matching backend routes/endpoints using method/path evidence, services to dependencies, and background workers to their observed triggers/queues where evidence exists. Preserve unresolved links as diagnostics rather than speculative edges.

## Steps
1. Add tests for matching normalized HTTP method/path pairs across C# and TypeScript symbols.
2. Add tests for component-to-route, component-to-service, service injection/use, and worker/queue evidence.
3. Generate deterministic, deduplicated relationships and diagnostics for ambiguous or unmatched calls.
4. Validate by running focused tests and building all affected projects.

**Done when**: Cross-layer test fixtures form connected, deterministic UI-to-service-to-API workflow graphs and accurately report unresolved/ambiguous edges; affected builds are warning-free.

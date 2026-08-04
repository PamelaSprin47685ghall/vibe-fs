# Architecture

Binding rules live in [`spec/`](../spec/). This page is orientation only.

## Layers

- Kernel / Domain: pure rules, facts, projections — no Host I/O.
- Application: orchestration, prompting, reconciliation programs (e.g. `Application/Orchestration/Program.fs`).
- Infrastructure: OpenCode hooks and adapters (`Infrastructure/OpenCode/Orchestration/`), Git, codecs, resource loaders (`Infrastructure/Resources/`).
- Session / Process: runtime cells, fallback, review, PTY ownership.

## DNA (spec/01)

1. Structured programs, not stage machines (ARCH-001).
2. Host events are wake signals; business truth from SDK snapshots (ARCH-002).
3. Do not modify OpenCode host (ARCH-003).

## Single writers

| Fact family | Writer |
|-------------|--------|
| Fallback cursor | `FallbackController` |
| User-shaped prompts | `PromptDispatcher` |
| PTY completion | backend `onExit` |
| Review confirm | derived from witness only |

## Context recovery

Failure-driven only (CTX-001 / CTX-002). No token budget estimation; no preemptive compaction.

## Package surface

- Entry: `dist/Infrastructure/OpenCode/Plugin/Plugin.js`
- Assets: `resources/prompts/*-system.md`, `resources/enforcer/catalog.json`
- Resource load: `Infrastructure/Resources/` (`PackageResources`, `PromptResources`, `EnforcerCatalogResource`, `RuntimeResources`) at plugin start

# PRD Index & Architecture Map: 万象术 (Wanxiangshu)

Welcome to the Product Requirement Documentation (PRD) directory for **万象术 (Wanxiangshu)** — a multi-agent plugin runtime compiled from F# to JavaScript via Fable.

This directory contains the authoritative technical specifications, behavioral contracts, state machine definitions, and architectural blueprints for the Wanxiangshu system.

---

## Architectural Axioms (第一性原理)

The engineering design of Wanxiangshu is governed by five foundational axioms:
1. **Stable Domain, Volatile Host**: Core rules live in the pure Kernel (`src/Kernel/`); host adapters only map wire formats.
2. **Event Sourcing as SSOT**: Durable progress is appended as facts to `.wanxiangshu.ndjson`. Memory states are folded from history.
3. **Strict Side-Effect Isolation**: All physical I/O (files, subprocesses, MCP client) is pushed to the Shell boundary (`src/Shell/`).
4. **Early Type Constraints**: Untrusted LLM parameters and host message payloads are parsed into static strongly-typed DUs immediately at the boundary.
5. **Time-Independent Test Reliability**: All E2E and integration tests must be deterministic and must never rely on system clocks, random seeds, or fragile sleeps. Test flow synchronization is managed via dependency injection and adaptive poll state hooks.

---

## 1. Document Taxonomy & Organization

All documentation files under `PRD/` follow a standardized, numbered, kebab-case naming convention and adhere to industry-standard product requirement document structures:

```text
PRD/
├── README.md                           # Directory Index, Navigation & Architecture Map
├── 01-master-spec.md                   # Master Product Requirement Document (Core System)
├── 02-event-sourcing.md                # Event Sourcing & Durable Persistence Specification
├── 03-continue-subagent.md             # Subagent Multi-Turn Continue Tool Specification
├── 03-fallback-recovery.md             # Model Fallback & Heuristic Degradation System PRD
├── 04-semble-mcp-injection.md          # Semble MCP & Investigator Breakpoint Injection Specification
├── 05-architecture-refactoring.md      # Type Safety & System Architecture Refactoring Roadmap
├── 07-hooks-complexity-audit.md        # Performance Hooks O(1) Complexity Audit Checklist
├── 08-amend-filter-spec.md              # Auto Amend (Backtracking) Mechanism Specification
├── 08-parallel-tools-and-empty-output.md # Parallel Tooling & Empty Output Fallback Spec
├── 09-context-budget-todowrite-trigger.md # Context Budget Active todowrite Trigger Spec
└── 10-context-budget-integration.md    # Context Budget Pipeline Integration Spec
```

---

## 2. Document Map & Matrix

| File | Document Title | Domain / Focus Area | Primary Audience |
| :--- | :--- | :--- | :--- |
| [`01-master-spec.md`](./01-master-spec.md) | **Master Product Specification** | Core Kernel/Shell architecture, Host adapters (OpenCode, Mimocode, Mux, OMP), With-Review Mode (`/loop`), WorkBacklog (`todowrite`), 54 Methodology Notebook Tools, Subagents, Tool Permissions & Catalog. | Architects, Engineers, QA |
| [`02-event-sourcing.md`](./02-event-sourcing.md) | **Event Sourcing & Persistence** | SSOT specification for `.wanxiangshu.ndjson`, file locking (`.wanxiangshu.ndjson.lock`), event types, state fold pure functions, and host compaction decoupling. | Core Developers, Storage Engineers |
| [`03-continue-subagent.md`](./03-continue-subagent.md) | **Subagent Multi-Turn Continue** | Subagent multi-turn session iterator store, continue tool interface, and polymorphic child session interaction protocols. | Core Developers, Plugin Developers |
| [`03-fallback-recovery.md`](./03-fallback-recovery.md) | **Fallback & Model Recovery** | Inner-loop fallback state machine, Perfect-Square heuristic algorithm, zero-timer design, AGENTS.md YAML models configuration, and event routing. | Infrastructure Engineers, Reliability Engineers |
| [`04-semble-mcp-injection.md`](./04-semble-mcp-injection.md) | **Semble MCP Injection Plan** | Best-effort stdio MCP client lifecycle, `investigator` context window breakpoint extraction, synthetic `read` message pair construction, and transform hook pipeline. | Plugin & Integration Developers |
| [`05-architecture-refactoring.md`](./05-architecture-refactoring.md) | **Architecture Refactoring Roadmap** | System defect diagnosis, 5-stage refactoring roadmap, DTO boundary defense (`Shell.Contracts`), `IHostAdapter` polyfill, and structured `DomainError` hierarchy. | System Architects, Tech Leads |
| [`07-hooks-complexity-audit.md`](./07-hooks-complexity-audit.md) | **Hooks O(1) Complexity Audit** | O(1) audit paths and complexity analysis of Mux/Opencode/Omp transform hooks, event log append buffer, list append replacements, and topology sort optimizations. | Core Developers, Performance Engineers |
| [`08-amend-filter-spec.md`](./08-amend-filter-spec.md) | **Auto Amend Backtracking** | Before-Hook tool args filtering, O(N) stack pruning fold recursive traversal, and callID-associated parallel tool results atomic pop boundaries. | Core Developers, QA |
| [`08-parallel-tools-and-empty-output.md`](./08-parallel-tools-and-empty-output.md) | **Parallel Tooling & Empty Output** | warn_tdd value upgrade, single tool call detection and synthetic parallel prompt injections, and empty output fallback recovery transitions. | Core Developers, QA |
| [`09-context-budget-todowrite-trigger.md`](./09-context-budget-todowrite-trigger.md) | **Context Budget todowrite Trigger** | Mathematical formulation of active backlog folding conditions, beginPhase estimation formulas, and 80% compaction fallback guards. | Core Developers, Storage Engineers |
| [`10-context-budget-integration.md`](./10-context-budget-integration.md) | **Context Budget Integration** | MessageTransformPipeline applyContextBudget integration hooks, nudge lock state, and context budget store definition. | Core Developers, Plugin Developers |

---

## 3. System Architecture Overview

```mermaid
graph TD
    SubGraphHost[Host Adapters Scope]
    OpenCode[OpenCode Plugin] --> ShellLayer[Shell Boundary Layer]
    Mimocode[Mimocode Plugin] --> ShellLayer
    Mux[Mux Plugin Catalog] --> ShellLayer
    OMP[Omp Extension] --> ShellLayer

    SubGraphShell[Shell Layer Scope]
    ShellLayer --> EventLogIO[EventLog File I/O & Locks]
    ShellLayer --> CodecRegistry[Tool & Args Codec Registry]
    ShellLayer --> TransformPipeline[Message Transform Pipeline]
    ShellLayer --> SembleMCP[Semble MCP Client]

    SubGraphKernel[Kernel Pure Rules Scope]
    EventLogIO --> EventFold[Event Log Fold Engine]
    CodecRegistry --> KernelCore[Kernel Pure Domain Rules]
    TransformPipeline --> KernelCore
    
    KernelCore --> ReviewFSM[ReviewSession FSM]
    KernelCore --> FallbackFSM[Fallback State Machine]
    KernelCore --> BacklogCore[WorkBacklog Projection Core]
    KernelCore --> ToolPermissions[Tool Permission Matrix]
    KernelCore --> Methodologies[54 Methodology Schemas]
```

---

## 4. Standard PRD Section Template

Every detailed PRD document in this directory adheres to the standardized 6-section structure:

1. **Product Overview**: One-Line Definition, Background & Motivation, Problem Statement, Architectural Axioms, Value Proposition.
2. **User Roles & Workflows**: User/Agent Roles, Interaction Models, Command Interfaces, End-to-End User Journeys.
3. **Functional Requirements**: Detailed Feature Specifications, State Machine Transitions, API Contracts, Input/Output Schemas, Business Rules.
4. **Technical & Data Specs**: Module & File Maps, Event Payload Definitions, Persistence Specifications, Wire Protocols, Codecs.
5. **Non-Functional Requirements**: Performance Ceilings, Concurrency Controls, Security & SSRF Protection, Architectural Invariants (`ArchitectureTests*`).
6. **Verification & Acceptance Criteria**: Test Suites, Integration Probes, E2E Verification Scenarios, Acceptance Criteria.

---

## 5. Source Code Alignment & Verification

The specifications in this directory are directly grounded in the source code of the workspace:
- **Kernel Pure Domain**: `src/Kernel/`
- **Shell & I/O Boundary**: `src/Shell/`
- **Methodology Catalog**: `src/Methodology/`
- **Host Adapters**: `src/Opencode/`, `src/Mux/`, `src/Omp/`
- **Verification Suites**: `tests/`, `e2e/`

For build and verification commands, refer to [`01-master-spec.md Section 6`](./01-master-spec.md#6-verification--acceptance-criteria).

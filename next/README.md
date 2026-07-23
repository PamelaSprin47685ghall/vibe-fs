# Wanxiangshu Next (`next/`) Formal Specification & Architecture Manual

> Next-Generation Multi-Agent Plugin Runtime for OpenCode — Event-Sourced Structured Flow Architecture in F# (Fable → JS).

---

## 1. Product Scope & OpenCode Target Directive

### 1.1 Target Host Policy
The `next/` architecture targets **OpenCode EXCLUSIVELY**. All previous host support and multi-host abstractions have been permanently eliminated:
- **REMOVED**: Mux host runtime (`src/Hosts/Mux/`)
- **REMOVED**: OMP / oh-my-pi host runtime (`src/Hosts/Omp/`)
- **REMOVED**: Mimocode & MimoTui host exports
- **REMOVED**: Legacy `.wanxiangshu.ndjson` storage format & workspace lockfiles
- **REMOVED**: Legacy Nudge Manager / Lease / Claim / Owner / Generation调度, SubsessionActor, & Fallback State Machine Tower

### 1.2 Supported User Capabilities
The runtime preserves full functional parity for developer capabilities:
1. **Multi-Agent Coordination**: Main, Coder, Inspector, Browser, Meditator, Reviewer, and Squad Slave execution scopes.
2. **Todo & Progress Handoff**: Formal Todo snapshots with status tracking and work report handoffs.
3. **With-Review Loop**: Automated code review rounds (`/loop`), verdict submission, and task revision cycling.
4. **Tail-Recursive Fallback**: Multi-model retry chains with zero-width continuation prompt correlation.
5. **File & System Tools**: Atomic file IO, fuzzy searching, ripgrep integration, and process isolation.
6. **PTY Terminal Management**: Native PTY process spawning, throttle-buffered reading, interactive writing, and lifecycle cleanup.
7. **Context & Compaction**: Automatic context budget enforcement, synthetic message transforms, and summary compaction.
8. **Fault Recovery**: Event-sourced state reconstruction from per-runtime NDJSON logs.
9. **Wanxiangzhen Squad Orchestration**: Requirement decomposition, DAG wave scheduling, isolated worktrees, slave OpenCode instances, verification gates, and fast-forward git merging (`wanxiangshu/wanxiangzhen`).

---

## 2. The Six Core Architectural Invariants

Every subsystem in `next/` MUST strictly enforce the following six invariants:

```
+-----------------------------------------------------------------------------------+
| 1. Journal-First (先盘后内存)                                                      |
|    appendSync -> flush -> Fold to local projection -> Reply Committed              |
+-----------------------------------------------------------------------------------+
| 2. Per-Runtime Single Writer                                                      |
|    File: .wanxiangshu/runtimes/<RuntimeId>.ndjson                                 |
+-----------------------------------------------------------------------------------+
| 3. Single Driver Worker per Session                                               |
|    OpenCode Hooks perform Decode -> TryPost -> Return (Zero long flow in Hooks)   |
+-----------------------------------------------------------------------------------+
| 4. Host MessageId-Correlated Prompts                                              |
|    assistant.parentID -> UserMessageId -> PromptKey -> Wake unique waiter          |
+-----------------------------------------------------------------------------------+
| 5. CommandPort Mutating Boundary                                                  |
|    Tools submit Session state mutations strictly via SessionCommandPort            |
+-----------------------------------------------------------------------------------+
| 6. Domain Flow Sequentiality                                                      |
|    Business logic steps preserve linear domain order without framework noise      |
+-----------------------------------------------------------------------------------+
```

---

## 3. Formal Schema & Type Specifications

### 3.1 Todo Snapshot & Item Schema

```fsharp
namespace Wanxiangshu.Next.Kernel

module Todo =

    type TodoStatus =
        | Pending
        | InProgress
        | Completed
        | Cancelled

    type TodoPriority =
        | Low
        | Medium
        | High
        | None

    type TodoItem =
        { Content: string
          Status: TodoStatus
          Priority: TodoPriority }

    type MethodologyId = string
    type WorkReport = string

    type TodoSnapshot =
        { Items: TodoItem list
          SelectedMethodologies: MethodologyId list
          Handoff: WorkReport option }

    type TodoView =
        { Snapshot: TodoSnapshot option
          Unfinished: bool }
```

### 3.2 Review Verdict Schema

```fsharp
namespace Wanxiangshu.Next.Kernel

module Review =

    [<RequireQualifiedAccess>]
    type ReviewVerdict =
        | Passed
        | NeedsChanges of changeRequests: string list
        | Invalid of reason: string
        | WorkInProgress

    type ReviewFact =
        | ReviewApplied of
            {| Verdict: ReviewVerdict
               Round: int
               ResultingTodo: TodoSnapshot option |}

    type ReviewView =
        { Required: bool
          Round: int
          Verdict: ReviewVerdict option }
```

### 3.3 Fallback Specification & Retry Schema

```fsharp
namespace Wanxiangshu.Next.Session

module Fallback =

    type FallbackConfig =
        { FallbackModels: string list
          MaxRetriesPerModel: int }

    type SendOutcome =
        | Delivered of messageId: string
        | Retryable of reason: string
        | Fatal of reason: string
        | AcceptanceUnknown of reason: string * messageId: string option

    type SendContinueFunction = string -> int -> SessionFlow<SendOutcome>

    // Tail-recursive execution over model chain and retry count
    val tryAttempts: SessionScript -> SendContinueFunction -> string -> int -> SessionFlow<SendOutcome option>
    val tryModels: SessionScript -> SendContinueFunction -> string list -> SessionFlow<SendOutcome>
```

### 3.4 Tool Permission Matrix

| Role | `todowrite` | `read`/`write`/`edit` | `executor` | `pty_*` | `fuzzy_*` | `web_*` | `subagent_*` | `submit_review` | `return_reviewer` |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **main** | Yes | Yes | Yes | Yes | Yes | Yes | Yes | Yes | No |
| **coder** | Yes | Yes | Yes | Yes | Yes | No | No | No | No |
| **inspector** | No | Read Only | No | No | Yes | No | No | No | No |
| **browser** | No | Read Only | No | No | No | Yes (MCP) | No | No | No |
| **meditator** | Yes | Read Only | No | No | Yes | No | No | No | No |
| **reviewer** | No | Read Only | No | No | Yes | No | No | No | Yes |
| **squad slave** | Yes | Yes | Yes | Yes | Yes | No | No | No | No |

### 3.5 Wanxiangzhen Squad Interface

```fsharp
namespace Wanxiangshu.Next.Wanxiangzhen

open System
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Session

type SquadTask =
    { TaskId: string
      TargetAgent: string
      Prompt: string }

type SquadWave =
    { WaveIndex: int
      Tasks: SquadTask list }

type SquadPlan =
    { Waves: SquadWave list }

type VerifiedResult =
    { TaskId: string
      Result: SquadTaskResult }

type SquadOutcome =
    | SquadCompleted of summary: string
    | SquadFailed of error: string

type SquadScript =
    { GetProgressStamp: unit -> int64
      CreateWorktree: SquadTask -> SquadFlow<IAsyncDisposable>
      StartSlave: IAsyncDisposable -> SquadTask -> SquadFlow<ChildSession>
      Verify: ChildResult -> SquadFlow<SquadTaskResult>
      PublishVerified: IAsyncDisposable -> SquadTaskResult -> SquadFlow<VerifiedResult>
      MergeOrder: VerifiedResult list -> VerifiedResult list
      FastForward: VerifiedResult -> SquadFlow<unit>
      AcceptWave: VerifiedResult list -> SquadFlow<unit>
      Complete: unit -> SquadFlow<SquadOutcome>
      RunParallel: SquadTask list -> (SquadTask -> SquadFlow<VerifiedResult>) -> SquadFlow<VerifiedResult list> }
```

---

## 4. Legacy OpenCode Behavior Ledger Summary (237 Items)

All 237 legacy OpenCode functional behaviors are cataloged in `docs/migration/behavior-ledger.json` and `docs/migration/behavior-ledger.generated.md`.

| Category | Count | Architectural Decision | New Module Owner |
| :--- | :---: | :--- | :--- |
| **BOOT** | 10 | **Keep** — Plugin startup, agent config loading, hook registration | `src/OpenCode/Plugin.fs`, `src/Journal/Gateway.fs` |
| **SCHEMA** | 9 | **Keep** — Static tool catalog & OpenAPI schema injection | `src/Tools/Catalog.fs`, `src/Tools/Types.fs` |
| **PERM** | 5 | **Keep** — Role-based tool permission enforcement | `src/Tools/Permission.fs` |
| **MSG** | 10 | **Keep** — OpenCode event codec & message origin decoding | `src/OpenCode/Decode.fs`, `src/OpenCode/Types.fs` |
| **FILE** | 13 | **Keep & Rewrite** — Atomic write, read limits, warn_tdd injection | `src/Tools/File.fs` |
| **FUZZY** | 10 | **Keep & Rewrite** — Stateful iterator fuzzy_find & grep | `src/Tools/Search.fs` |
| **EXEC** | 13 | **Keep & Rewrite** — Process spawning, deadline & output bounds | `src/Tools/Executor.fs`, `src/Process/` |
| **PTY** | 11 | **Keep & Rewrite** — Terminal handle table & throttle reading | `src/Tools/Pty.fs`, `src/Process/Pty.fs` |
| **WEB** | 12 | **Keep & Rewrite** — Web search, fetch SSRF guard & browser MCP | `src/Tools/Web.fs` |
| **SUB** | 15 | **Replace** — Subagent dispatch (Replaced SubsessionActor with Child.fs) | `src/Session/Child.fs`, `src/Tools/Subagent.fs` |
| **CONT** | 10 | **Replace** — Continuation handles (Replaced with PromptProtocol) | `src/Session/PromptProtocol.fs`, `src/Session/Script.fs` |
| **REV** | 20 | **Keep & Rewrite** — Automated review loop & verdict submission | `src/Session/Review.fs`, `src/Tools/Review.fs` |
| **NUDGE** | 15 | **Replace** — Nudge continuation (Moved to Session Flow; dropped Lease/Owner) | `src/Session/Script.fs`, `src/Session/Driver.fs` |
| **FB** | 24 | **Replace** — Fallback retry (Replaced State Machine Tower with tail-recursion) | `src/Session/Fallback.fs` |
| **COMP** | 12 | **Keep** — Compaction summary & synthetic message transforms | `src/Tools/MessageTransform.fs` |
| **ES** | 15 | **Replace** — Event Sourcing (Replaced global .wanxiangshu.ndjson with Per-Runtime Journal) | `src/Journal/Writer.fs`, `src/Journal/Fold.fs` |
| **LIFE** | 15 | **Keep & Rewrite** — Human turn, prompt protocol & disposal | `src/Session/Runtime.fs`, `src/Session/Driver.fs` |
| **CONC** | 12 | **Keep** — Session isolation & concurrent tool call handling | `src/Session/Runtime.fs`, `src/Session/Inbox.fs` |
| **STAB** | 6 | **Keep** — Stability gates, leak detection & static assertions | `tests/Gates/` |

---

## 5. Build, Test, and Verification Commands

```bash
# Build next Fable outputs to build/next
npm run build:next

# Run full unit and integration test suite for next
npm run test:next
```

# HNI port registry

Single source of truth for host-neutral ports. **Rebase and read this file before adding or changing a port.**
Add/modify the row in the same commit as the code. Names and shapes follow [DESIGN.md](DESIGN.md) §1–§2.
Status: PLANNED (designed, no code) · DECLARED (`.fsi` exists in `src/Wanxiangshu/Host/`) · WIRED-OC (OpenCode
adapter implements it) · WIRED-OMP (omp adapter skeleton conforms) · LEGACY-GONE (old host-shaped contract deleted).

## Ports

| ID | Port (namespace `Wanxiangshu.Host.Ports`) | File | Owner lane | Replaces (legacy) | Status |
|---|---|---|---|---|---|
| P-01 | `ISessionPort`, `SessionSpec`, `SessionInfo` | `Host/Ports/Sessions.fs(i)` | fork (+ Execution/Session) | `IOpenCodePort` session members, `ISessionHostPort` create/list/parent, `OpenCodeChildOptions`, `OpenCodeChildInfo`, `ISessionDiscoveryPort` | DECLARED, WIRED-OMP |
| P-02 | `IPromptPort`, `PromptRequest`, `DeliveryVerdict` | `Host/Ports/Prompts.fs(i)` | fission (+ Interaction/Dispatch) | `OpenCode.IPromptPort`, `OpenCodePromptOptions`, `DetachedSendVerdict` | DECLARED, WIRED-OMP |
| P-03 | `ITranscriptPort`, `TranscriptMessage`, `TranscriptPart` | `Host/Ports/Transcript.fs(i)` | fork (+ Execution/Session) | `ISessionSnapshotPort`, `SessionMessage` | DECLARED, WIRED-OMP |
| P-04 | `IToolRegistrar`, `ToolSpec`, `ToolInvocation`, `ToolOutcome` | `Host/Ports/Tools.fs(i)` | tools | `OpenCode/Tools/ToolContext` `Tool`/`ToolContext`/`ToolOutput` | DECLARED, WIRED-OMP |
| P-05 | `IToolGate`, `ToolAdmission`, `ToolResultEdit`, `ToolDefinitionEdit` | `Host/Ports/ToolGates.fs(i)` | tools | `tool.execute.before/after`, `tool.definition` handling in `OpenCode/Plugin/PluginHooks` | DECLARED, WIRED-OMP |
| P-06 | `ITurnTransform`, `TurnAdmission`, `ContextEdit`, `ParamsVerdict` | `Host/Ports/TurnTransforms.fs(i)` | plugin-host | `OpenCode/Plugin/PluginTransforms`, `ChatParamsHook`, `ProviderSystemTransform` entry points | DECLARED, WIRED-OMP |
| P-07 | `ISignalSource`, `HostSignal`, `TurnTerminal` | `Host/Ports/Signals.fs(i)` | plugin-host | `OpenCode/Signals/HostSignal`, `HostEventCodec` terminal types | DECLARED, WIRED-OMP |
| P-08 | `IModelCatalog`, `ModelRoute`, `SpawnRoute` | `Host/Ports/ModelRouting.fs(i)` | plugin-host | `OpencodeModel` (Foundation), `ModelRoutingSurface` host calls | DECLARED, WIRED-OMP |
| P-09 | `ICompactionGate`, `CompactionContribution`, `ContinueVerdict` | `Host/Ports/Compaction.fs(i)` | plugin-host | `HostCompactionGate` hook entry | DECLARED, WIRED-OMP |
| P-10 | `IHostPaths` | `Host/Ports/Storage.fs(i)` | arch | ad-hoc `input.directory`/`project` reads | DECLARED, WIRED-OMP |
| P-11 | `IHostPlugin`, `HostPorts`, `ProductHooks`, `HostCapabilities`, `IAgentCatalog` | `Host/Ports/Lifecycle.fs(i)` | plugin-host | `OpenCode/Plugin/Plugin`, `PluginBoot`, `ManagedAgentConfig` host side | DECLARED, WIRED-OMP |
| P-12 | `IDiagnosticSink` | `Host/Ports/Diagnostics.fs(i)` | plugin-host | `client.app.log` call sites | DECLARED, WIRED-OMP |
| P-00 | `HostError`, `HostResult`, `HostCapability`, `WorkspacePath`, `AgentRole`, `CanonicalJsonText`, `JsonSchema`, `HostFailure`, `TurnTerminal` (shared vocabulary; ids `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress` come from `Foundation.Identity`) | `Host/Ports/Vocabulary.fs(i)` | arch | `string` error channels on host ports | DECLARED, WIRED-OMP |

## Adapter folders (implementations, not ports)

| Host | Folder | Owner lane |
|---|---|---|
| OpenCode | `src/Wanxiangshu/OpenCode/**`, `src/Wanxiangshu/**/OpenCode/**` | per domain lane (fission, fork, tools, plugin-host) |
| omp | `proposals/host-neutral-interface/adapters/omp/` (skeleton, offline fixtures) → `src/Wanxiangshu/Omp/` once wired | arch |

## Domain lanes (owner directive 06:05 UTC)

| Lane | Scope | Ports it may extend |
|---|---|---|
| arch (eng-host-arch) | DESIGN/REGISTRY/BOARD, P-00, P-10, omp skeleton, thinness gate, drift review | all (review) |
| fission | `Execution/Fission/OpenCode` | P-02 (+ P-01 read-only use) |
| fork | `Execution/Delegation/Fork/OpenCode` + other `Execution/*/OpenCode` | P-01, P-03 |
| tools | `OpenCode/Tools` ToolRegistry/ToolRuntimeScope + `OpenCode/Codec` | P-04, P-05 |
| plugin-host | `OpenCode/Plugin` + `OpenCode/Host` | P-06, P-07, P-08, P-09, P-11, P-12 |
| unclaimed | `Mission/Work/OpenCode`, `Repository/**/OpenCode`, `Sphinx/V2/Hosts/OpenCode`, `Context/Prefix/OpenCode`, `Interaction/Dispatch/OpenCode` | claim here (edit this row) |

## Change log
- 2026-10-21 eng-host-arch: initial registry (P-00..P-12 PLANNED).
- 2026-10-21 eng-host-arch (HNI-02/03): all families DECLARED in shard `Wanxiangshu.Owner.host-boundary.host-neutral-ports.fsproj`
  (types only, no consumers yet); omp skeleton conforms (WIRED-OMP = type-checked + offline fixture, not shipped).
  Renamed before any consumer existed: `ToolCall`→`ToolInvocation`, `ToolResult`→`ToolOutcome`, transcript part `ToolCall`→`ToolUse`
  (the old names collide with existing union cases and trip shard-edge-audit). No port is WIRED-OC yet.

## How to add or change a port (domain lanes)
1. Rebase, read this file and the family `.fsi`. Prefer extending an existing family over a new file.
2. Port files reference only `Wanxiangshu.Foundation*`, `Wanxiangshu.Host*`, `System*` (gate ⑪ port-purity, strict).
3. Reuse vocabulary: `HostResult<_>`/`HostError` for host round trips, `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress`
   ids, `ModelRoute`, `AgentRole`, `CanonicalJsonText`. No `obj`, no `string` error channel, no host wire term.
4. Add the type/member to `proposals/host-neutral-interface/adapters/omp/ports.ts` too (`adapters/omp/check.sh` fails on drift), or
   leave it out and note "omp: n/a" here — the arch lane then completes the mirror.
5. Update the row (Status, Replaces) in the same commit. Mark WIRED-OC when the OpenCode adapter implements it.

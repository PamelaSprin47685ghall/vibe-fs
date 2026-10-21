# HNI port registry

Single source of truth for host-neutral ports. **Rebase and read this file before adding or changing a port.**
Add/modify the row in the same commit as the code. Names and shapes follow [DESIGN.md](DESIGN.md) §1–§2.
Status: PLANNED (designed, no code) · DECLARED (`.fsi` exists in `src/Wanxiangshu/Host/`) · WIRED-OC (OpenCode
adapter implements it) · SKELETON-OMP (the omp adapter skeleton type-checks against the port and passes an offline
fixture; **not** wired to a running host, not shipped) · LEGACY-GONE (old host-shaped contract deleted).

## Ports

| ID | Port (namespace `Wanxiangshu.Host.Ports`) | File | Owner lane | Replaces (legacy) | Status |
|---|---|---|---|---|---|
| P-01 | `ISessionPort`, `SessionSpec`, `SessionInfo` | `Host/Ports/Sessions.fs(i)` | fork (+ Execution/Session) | `IOpenCodePort` session members, `ISessionHostPort` create/list/parent, `OpenCodeChildOptions`, `OpenCodeChildInfo`, `ISessionDiscoveryPort` | DECLARED, SKELETON-OMP |
| P-02 | `IPromptPort`, `PromptRequest`, `DeliveryVerdict` | `Host/Ports/Prompts.fs(i)` | fission (+ Interaction/Dispatch) | `OpenCode.IPromptPort`, `OpenCodePromptOptions`, `DetachedSendVerdict` | DECLARED, SKELETON-OMP |
| P-03 | `ITranscriptPort`, `TranscriptMessage`, `TranscriptPart` | `Host/Ports/Transcript.fs(i)` | fork (+ Execution/Session) | `ISessionSnapshotPort`, `SessionMessage` | DECLARED, SKELETON-OMP |
| P-04 | `IToolRegistrar`, `ToolSpec`, `ToolInvocation`, `ToolOutcome` | `Host/Ports/Tools.fs(i)` | tools | `OpenCode/Tools/ToolContext` `Tool`/`ToolContext`/`ToolOutput` | DECLARED, SKELETON-OMP |
| P-05 | `IToolGate`, `ToolCallVerdict`, `ToolResultEdit`, `ToolDefinitionEdit` | `Host/Ports/ToolGates.fs(i)` | tools | `tool.execute.before/after`, `tool.definition` handling in `OpenCode/Plugin/PluginHooks` | DECLARED, SKELETON-OMP |
| P-06 | `ITurnTransform`, `TurnAdmission`, `ContextEdit`, `ParamsVerdict` | `Host/Ports/TurnTransforms.fs(i)` | plugin-host | `OpenCode/Plugin/PluginTransforms`, `ChatParamsHook`, `ProviderSystemTransform` entry points | DECLARED, SKELETON-OMP |
| P-07 | `ISignalSource`, `HostSignal`, `TurnTerminal` | `Host/Ports/Signals.fs(i)` | plugin-host | `OpenCode/Signals/HostSignal`, `HostEventCodec` terminal types | DECLARED, SKELETON-OMP |
| P-08 | `IModelCatalog`, `ModelRoute`, `SpawnRoute` | `Host/Ports/ModelRouting.fs(i)` | plugin-host | `OpencodeModel` (Foundation), `ModelRoutingSurface` host calls | DECLARED, SKELETON-OMP |
| P-09 | `ICompactionGate`, `CompactionContribution`, `ContinueVerdict` | `Host/Ports/Compaction.fs(i)` | plugin-host | `HostCompactionGate` hook entry | DECLARED, SKELETON-OMP |
| P-10 | `IHostPaths` | `Host/Ports/Storage.fs(i)` | arch | ad-hoc `input.directory`/`project` reads | DECLARED, SKELETON-OMP |
| P-11 | `IHostPlugin`, `HostPorts`, `ProductHooks`, `HostCapabilities`, `IAgentCatalog` | `Host/Ports/Lifecycle.fs(i)` | plugin-host | `OpenCode/Plugin/Plugin`, `PluginBoot`, `ManagedAgentConfig` host side | DECLARED, SKELETON-OMP |
| P-12 | `IDiagnosticSink` | `Host/Ports/Diagnostics.fs(i)` | plugin-host | `client.app.log` call sites | DECLARED, SKELETON-OMP |
| P-00 | `HostError`, `HostResult`, `HostCapability`, `WorkspacePath`, `AgentRole`, `CanonicalJsonText`, `JsonSchema`, `HostFailure`, `TurnTerminal` (shared vocabulary; ids `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress` come from `Foundation.Identity`) | `Host/Ports/Vocabulary.fs(i)` | arch | `string` error channels on host ports | DECLARED, SKELETON-OMP |

## Lane-declared domain ports (namespace `Wanxiangshu.Host`, files `Host/*.fs(i)`) — ID reservations

Domain lanes declare seams their domain needs from a host in `src/Wanxiangshu/Host/<Name>.fs(i)`. IDs are reserved here
so parallel lanes never collide (HNI-11 found two `P-13`s). Use the ID below in your row; next free ID: **P-19**.

| ID | Port | File | Owner lane | Notes (HNI-11 review) |
|---|---|---|---|---|
| P-13 | `CommandDeclaration`, `CommandArgument`, `ArgumentShape`, `ICommandArguments` | `Host/CommandDeclaration.fs(i)` | fork | Neutral and product-shaped. Converges with P-04: `ArgumentShape` is what a `ToolSpec.Schema` is generated from. |
| P-14 | `IDelegationCallerHost`, `DelegationCaller` | `Host/DelegationCaller.fs(i)` | fork | D-03: carries `AgentJournal` (product persistence) and fork runtime — a composition seam, not a host port. |
| P-15 | `IDelegationCommandHost`, `DelegationReply` | `Host/DelegationCommand.fs(i)` | fork | D-03 as P-14. |
| P-16 | `PairGuidanceTranscript`, `PairGuidanceLedger`, `PairGuidanceOccurrence` | `Host/PairGuidance.fs(i)` | plugin-host | Was proposed as "P-13" (collision D-01). Feeds P-06 `ContextMessages`. |
| P-17 | `FissionHostPort`, `FissionOwnerRuns` | `Host/FissionPort.fs(i)` | fission | D-03: carries `AgentJournal`, legacy `ISessionHostPort`, `Execution.Session` types. |
| P-18 | `HostToolContext`, `ToolAdmission` (declared authority: `OfficeRole` \| `PrivateAttachment`), `ToolResultText` | `Host/Tools.fs(i)` | tools | Was proposed as a redefinition of "P-04" (D-02). P-04 stays the registration port in `Host/Ports/Tools.fs`; P-05's call verdict is `ToolCallVerdict` (renamed to match the tools lane). |

### Drift findings (HNI-11, 2026-10-21) and the convergence target
- **D-01** ID collision (`P-13` used by fork and plugin-host) → plugin-host pair guidance is **P-16**.
- **D-02** tools lane redefined P-04 → recorded as **P-18**. Convergence: `HostToolContext` and `Ports.ToolInvocation` describe the
  same call; target one type with typed `SessionId` (not `string`), `AgentRole` (not `string`), cancellation as a token.
  `ToolResultText` (TOML rendering of product results) is product prose, not a host seam → move to a product module.
- **D-03** P-14/P-15/P-17 hand the domain an `AgentJournal` and other product runtime objects "from the host". A host never owns
  product state (DESIGN §2 P-10). Convergence: split each into (a) neutral host ports from `HostPorts` (P-01 sessions, P-02 prompts,
  P-07 signals) and (b) product-composition inputs supplied by `Composition/`. Card HNI-14.
- **D-04** lane ports use `Result<_, string>` for host round trips; neutral ports use `HostResult<_>` (`HostError`). Convert when the
  lane switches from legacy `ISessionHostPort` to P-01.
- Lane domain ports are not under gate ⑪ (port-purity covers `Host/Ports/` only); HNI-14 moves them there once D-03 is resolved.

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
  (types only, no consumers yet); omp skeleton conforms (SKELETON-OMP = generated mirror byte-identical to the F# signatures, type-checked against the omp 18.12.4 types, offline fixture green; **not** wired to a running host, not shipped).
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

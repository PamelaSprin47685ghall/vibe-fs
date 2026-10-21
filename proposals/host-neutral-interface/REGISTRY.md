# HNI port registry

Single source of truth for host-neutral ports. **Rebase and read this file before adding or changing a port.**
Add/modify the row in the same commit as the code. Names and shapes follow [DESIGN.md](DESIGN.md) §1–§2.
Status: PLANNED (designed, no code) · DECLARED (`.fsi` exists in `src/Wanxiangshu/Host/`) · WIRED-OC (OpenCode
adapter implements it) · SKELETON-OMP (the omp adapter skeleton type-checks against the port and passes an offline
fixture; **not** wired to a running host, not shipped) · LEGACY-GONE (old host-shaped contract deleted).

## Ports

| ID | Port (namespace `Wanxiangshu.Host.Ports`) | File | Owner lane | Replaces (legacy) | Status |
|---|---|---|---|---|---|
| P-01 | `ISessionPort`, `SessionSpec`, `SessionInfo` | `Host/Ports/Sessions.fs(i)` | fork (+ Execution/Session) | `IOpenCodePort` session members, `ISessionHostPort` create/list/parent, `OpenCodeChildOptions`, `OpenCodeChildInfo` | DECLARED, WIRED-OC, SKELETON-OMP |
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

**These lane ports are not on `master` yet.** As of 2026-10-21 they live only on their lanes' branches
(`origin/wip/hni-tools`, `origin/wip/hni-fork`, `origin/wip/hni-plugin`, `origin/wip/hni-fission`); `master` has no
`Host/Ports/` consumers and none of these files. The rows below are ID reservations plus the shape review —
they are not a claim that the files are merged. Namespace note: the lane ports use `Wanxiangshu.Host`, while the
neutral ports use `Wanxiangshu.Host.Ports` (see DESIGN §1.1 and drift **D-05**).

| ID | Port | File | Owner lane | Notes (HNI-11 review) |
|---|---|---|---|---|
| P-13 | `CommandDeclaration`, `CommandArgument`, `ArgumentShape`, `ICommandArguments` | `Host/CommandDeclaration.fs(i)` | fork | Neutral and product-shaped. Converges with P-04: `ArgumentShape` is what a `ToolSpec.Schema` is generated from. Status: WIRED-OC. |
| P-14 | `IDelegationCallerHost`, `DelegationCaller` | `Host/DelegationCaller.fs(i)` | fork | D-03: carries `AgentJournal` (product persistence) and fork runtime — a composition seam, not a host port. Status: WIRED-OC. |
| P-15 | `IDelegationCommandHost`, `DelegationReply` | `Host/DelegationCommand.fs(i)` | fork | D-03 as P-14. Status: WIRED-OC. |
| P-16 | `PairGuidanceTranscript`, `PairGuidanceLedger`, `PairGuidanceOccurrence` | `Host/PairGuidance.fs(i)` | plugin-host | Was proposed as "P-13" (collision D-01). Feeds P-06 `ContextMessages`. |
| P-17 | `FissionHostPort`, `FissionOwnerRuns` | `Host/FissionPort.fs(i)` | fission | D-03: carries `AgentJournal`, legacy `ISessionHostPort`, `Execution.Session` types. |
| P-18 | `HostToolContext`, `ToolAdmission` (declared authority: `OfficeRole` \| `PrivateAttachment`); `ToolResultText` is a **module** (product prose), not a type | `Host/Tools.fs(i)` | tools | Was proposed as a redefinition of "P-04" (D-02). P-04 stays the registration port in `Host/Ports/Tools.fs`; P-05's call verdict is `ToolCallVerdict` (renamed to match the tools lane). **D-07: this file name collides with P-04's `Host/Ports/Tools.fs(i)` — rename before moving it under `Host/Ports/`.** |

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
- **D-05** lane ports declare `namespace Wanxiangshu.Host`, the same namespace many adapter files `open` for `CanonicalJson`/`HostDigest`.
  The neutral ports were moved to `Wanxiangshu.Host.Ports` for exactly that reason (DESIGN §1.1); the lane ports should move to a
  namespace that is not `open`ed by adapters (e.g. `Wanxiangshu.Host.Domain.<Lane>`) or the same shadowing risk applies to them.
- **D-06** the lane port files (`Host/*.fs(i)`) carry no `HNI P-xx` header marker. The TS mirror generator
  discovers the port families from `src/Wanxiangshu/Host/Ports/*.fsi` and keys on that marker; a family without one
  is a **hard error**, not a silent skip. The lane files sit directly under `Host/` today, so they are not
  discovered — but the moment a lane moves one under `Host/Ports/` (which HNI-14 intends) the mirror generation
  fails until the file declares its reserved id. A lane that lands a new family must add the `HNI P-xx` marker to
  the file header.
- **D-07** the tools lane's `Host/Tools.fs(i)` has the **same file name** as P-04's `Host/Ports/Tools.fs(i)`.
  Today the two live in different directories, so nothing breaks; but the moment the tools lane moves its file
  under `Host/Ports/` (which HNI-14 intends) it **overwrites P-04** — the registration port — and the
  `Tools.fs`/`Tools.fsi` pair the mirror generator reads would be the wrong one. The tools lane must rename its
  file (e.g. `Host/Ports/ToolContext.fs(i)`) before it moves it. No type name collides today: the tools lane
  declares `HostToolContext`/`ToolAdmission`, P-04 declares `ToolInvocation`/`ToolOutcome`; the **file name**
  is the collision, not the type name.
- Lane domain ports are not under gate ⑪ (port-purity covers `Host/Ports/` only); HNI-14 moves them there once D-03 is resolved.

### What a lane must do when it lands a domain port (the D-05/D-06/D-07 checklist)

These are the concrete steps, so a lane does not have to re-derive them:

1. **Move the file under `src/Wanxiangshu/Host/Ports/`** — that directory is what the mirror generator and
   gate ⑪ (port-purity) both look at. A file left directly under `Host/` is invisible to both.
2. **Rename the file if its base name is taken** by a neutral family. The names already taken are
   `Vocabulary`, `Sessions`, `Prompts`, `Transcript`, `Tools`, `ToolGates`, `TurnTransforms`, `Signals`,
   `ModelRouting`, `Compaction`, `Storage`, `Lifecycle`, `Diagnostics` (**D-07**). The family name is the
   `.fsi` base name, so it is also the TS mirror section name.
3. **Declare the reserved id in the header**: a line containing `HNI P-xx` where `xx` is your reserved id from
   the table above (P-13 … P-18). The generator matches `/\bHNI (P-\d{2})\b/` anywhere in the header; a family
   without one is a **hard error**, not a silent skip. Put it in the file's leading doc comment, e.g.
   `/// HNI P-18: product tool contribution — what one product tool call knows about its invocation.`
4. **Use a namespace that adapters do not `open`**: `Wanxiangshu.Host` is `open`ed by 76 files under
   `src/Wanxiangshu/` (mostly for `CanonicalJson`/`HostDigest`), so a type named e.g. `Digest` in that namespace
   shadows the real one at every one of those call sites. Use `Wanxiangshu.Host.Domain.<Lane>` (**D-05**). The
   neutral families use `Wanxiangshu.Host.Ports`, which nothing `open`s.
5. **Reuse the neutral vocabulary** (DESIGN §2): `HostResult<_>`/`HostError` instead of `Result<_, string>`
   (**D-04**), and `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress`/`ModelRoute`/`AgentRole`/
   `CanonicalJsonText` from `Host/Ports/Vocabulary.fs(i)` instead of `string`.
6. **Do not hand-edit** `adapters/omp/ports.ts`; it is generated. Run `adapters/omp/check.sh` — it regenerates
   the mirror and fails on any byte difference.
7. **Update your row** in the table above (Status, Replaces) in the same commit.

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
| fork | `Execution/Delegation/Fork/OpenCode` + other `Execution/*/OpenCode` | P-01, P-03, P-13, P-14, P-15 |
| tools | `OpenCode/Tools` ToolRegistry/ToolRuntimeScope + `OpenCode/Codec` | P-04, P-05 |
| plugin-host | `OpenCode/Plugin` + `OpenCode/Host` | P-06, P-07, P-08, P-09, P-11, P-12 |
| unclaimed | `Mission/Work/OpenCode`, `Repository/**/OpenCode`, `Sphinx/V2/Hosts/OpenCode`, `Context/Prefix/OpenCode`, `Interaction/Dispatch/OpenCode` | claim here (edit this row) |

## Change log
- 2026-10-21 eng-host-arch: initial registry (P-00..P-12 PLANNED).
- 2026-10-21 eng-host-arch (HNI-02/03): all families DECLARED in shard `Wanxiangshu.Owner.host-boundary.host-neutral-ports.fsproj`
  (types only, no consumers yet); omp skeleton conforms (SKELETON-OMP = generated mirror byte-identical to the F# signatures, type-checked against the omp 18.12.4 types, offline fixture green; **not** wired to a running host, not shipped).
  Renamed before any consumer existed: `ToolCall`→`ToolInvocation`, `ToolResult`→`ToolOutcome`, transcript part `ToolCall`→`ToolUse`
  (the old names collide with existing union cases and trip shard-edge-audit). No port is WIRED-OC yet.
- 2026-10-21 eng-hni-fork: P-13..P-15 WIRED-OC. Consumers: `Execution/Delegation/Fork/Command` (`ForkCommand`: fork/resume/commission
  rules, prose, receipts, argument declarations) and `Execution/Session/HorizonRoster` (horizon prose + data). Their members still answer
  `Result<_, string>` (the `HostForkRuntime`/`OrchestratorHost` error channel); moving to `HostError` waits for P-00.
  `DelegationCaller.Session` is `SessionId option` (rule 2): the OpenCode adapter maps a blank `sessionID` to None.
- 2026-10-21 eng-hni-fork: horizon joins P-14 (HorizonRoster holds the roster rules, prose and data rows; HorizonTool keeps its `HorizonRuntimeContext` API for ToolRegistry/ToolSurface and only binds the port and encodes the wire).
- 2026-10-21 eng-hni-fork: `Handle/OpenCode/JoinWake` had no host reference; it moves to `Execution/Delegation/Handle/JoinWake` (namespace `…Handle`), the `Handle/OpenCode` folder is gone, and three plugin-host files drop their unused `open …Handle.OpenCode`.
- 2026-10-21 eng-hni-fork: sync-delegate batching rules (batch resolution, deferred-inspection ledger, settled-result instructions, merged-reference prose) leave `SyncDelegate/OpenCode/Batching` for host-neutral `Execution/Delegation/SyncDelegate/Batching` (`SyncDelegateBatchRules`), a P-14 consumer: resolution reads the `DelegationCaller`, the ledger stores instruction lines. The adapter keeps only the HostToolContext → DelegationCaller mapping, the TOML reply and the OpenCode message-part mutation (279 → 102 lines); its API is unchanged.
- 2026-10-21 eng-hni-fork (slice B): the join wire rules leave `Fork/OpenCode/JoinTool` for host-neutral `Execution/Delegation/Fork/JoinRules`
  (`JoinRules`: `JoinRecord`, `decideCompletedStatus`, `mapForkError`, `mapJoinItem`, `mapOrchestratorVerdict`). **No new port**: the rules
  consume only product-domain types (`ForkError`, `JoinItem`, `Role`, `ManagerJobId`, `CommitHash`, `OrchestratorVerdict`), and the host capability
  they need is already bound through `ToolRuntimeScope` + P-14/P-15. Next free id stays **P-19**. The adapter keeps only the wiring
  (journal read for the manager job name, runtime name/role resolution, window rendering) and re-exports `JoinRecord` so `ToolSurface` and
  `ToolRegistry` are unchanged. `omp: n/a` (no port type, so the omp mirror is untouched).

## How to add or change a port (domain lanes)
1. Rebase, read this file and the family `.fsi`. Prefer extending an existing family over a new file.
2. Port files reference only `Wanxiangshu.Foundation*`, `Wanxiangshu.Host*`, `System*` (gate ⑪ port-purity, strict).
3. Reuse vocabulary: `HostResult<_>`/`HostError` for host round trips, `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress`
   ids, `ModelRoute`, `AgentRole`, `CanonicalJsonText`. No `obj`, no `string` error channel, no host wire term.
4. The TS mirror `proposals/host-neutral-interface/adapters/omp/ports.ts` is **generated** from the family `.fsi`
   files — never hand-edit it. `adapters/omp/check.sh` regenerates it and fails on any byte difference, so a
   changed type or member shows up as a mirror drift. If the change is not representable in the mirror, say
   "omp: n/a" in the row and the arch lane completes the mirror. The port families are discovered from the
   sources (each `Host/Ports/*.fsi` declares its reserved id in an `HNI P-xx` header marker), so a new family
   is mirrored automatically.
5. Update the row (Status, Replaces) in the same commit. Mark WIRED-OC when the OpenCode adapter implements it.

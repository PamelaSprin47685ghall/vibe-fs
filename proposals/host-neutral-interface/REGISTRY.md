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

**The fork lane's ports (P-13/P-14/P-15) are on `master`.** `Host/CommandDeclaration.fs(i)`,
`Host/DelegationCaller.fs(i)` and `Host/DelegationCommand.fs(i)` landed on `master` through the fork lane's six
commits `58660d4d9`, `2e9d5025d`, `6c6b8cfdf`, `a4222e3fb`, `e16e72c34` (slice A) and `aa43d3455` (slice B,
`Fork/JoinRules`). The fork lane's branches `origin/wip/hni-fork` and `origin/wip/hni-fork-next` are **deleted**
(their content is on `master`).
The other lanes' ports are **not all still off `master`**: P-16 (`Host/PairGuidance.fs(i)`) lives on
`origin/wip/hni-plugin` and P-18 (`Host/Tools.fs(i)`) on `origin/wip/hni-tools` — both branches still exist on the
remote (measured 2026-10-21). **P-17 (`Host/FissionPort.fs(i)`) is now on `master`**: the fission lane landed
`2680bc07d` ("Fission launch runs behind the host-neutral FissionHostPort (P-17)"), `0287a2349` ("P-17 FissionPort
moves to `Wanxiangshu.Host.Ports`, journal leaves the port") and `9894b1e0c` ("the fission host call sites use the
short FissionHost form"), and `origin/wip/hni-fission` is **deleted**.
`master` has **7 files outside the shard that `open Wanxiangshu.Host.Ports`** (measured at `31a5a2426`, 2026-10-21):
`OpenCode/Host/SessionSnapshotPort.fs`, `OpenCode/Host/SessionSnapshotPort.fsi`, `OpenCode/Host/SessionsSurface.fs`
and `OpenCode/Plugin/PluginRecoveryWiring.fs` — all four added by `4403f579b` ("session discovery behind P-01
ISessionPort") — plus `Execution/Fission/Launch.fs`, `Execution/Fission/Launch.fsi` and
`Execution/Fission/OpenCode/Tool.fs` from the HNI-05 fission lane. **An `open` is not an implementation**: the
neutral families are still *declared* types, and whether a port is actually wired is decided per row, not by the
presence of an `open`. Measured at `31a5a2426`, only **P-01** of P-01..P-12 has an implementation in `src/`
(`SdkSessionPort`/`HttpSessionPort` in `OpenCode/Host/SessionSnapshotPort.fs`); P-02..P-12 have none.
Namespace note: the lane ports use `Wanxiangshu.Host`, while the
neutral ports use `Wanxiangshu.Host.Ports` (see DESIGN §1.1 and drift **D-05**).

**No compile/test evidence for the fork lane.** The fork lane was pushed under the owner's "no build before push"
rule: only the non-compiling gates were run (`architecture.mjs`, `adapter-thinness.mjs`, `provider-leak-gate.mjs`).
The delivery team must build master at `aa43d3455` or later to get compile/test evidence for P-13/P-14/P-15.

| ID | Port | File | Owner lane | Notes (HNI-11 review) |
|---|---|---|---|---|
| P-13 | `CommandDeclaration`, `CommandArgument`, `ArgumentShape`, `ICommandArguments` | `Host/CommandDeclaration.fs(i)` | fork | Neutral and product-shaped. Converges with P-04: `ArgumentShape` is what a `ToolSpec.Schema` is generated from. Status: WIRED-OC, **on `master`** (`58660d4d9`). |
| P-14 | `IDelegationCallerHost`, `DelegationCaller` | `Host/DelegationCaller.fs(i)` | fork | D-03: carries `AgentJournal` (product persistence) and fork runtime — a composition seam, not a host port. Status: WIRED-OC, **on `master`** (`58660d4d9`). |
| P-15 | `IDelegationCommandHost`, `DelegationReply` | `Host/DelegationCommand.fs(i)` | fork | D-03 as P-14. Status: WIRED-OC, **on `master`** (`58660d4d9`). |
| P-16 | `PairGuidanceTranscript`, `PairGuidanceLedger`, `PairGuidanceOccurrence` | `Host/PairGuidance.fs(i)` | plugin-host | Was proposed as "P-13" (collision D-01). Feeds P-06 `ContextMessages`. Status: **not on `master`** — the file lives on `origin/wip/hni-plugin` (branch still exists, measured 2026-10-21). | Status **WIRED-OC**: the OpenCode adapter (`OpenCode/Host/PairProgrammingThoughtTransform`) decodes the transcript and renders only; the product owner is `Interaction/PairGuidance/Occurrences` (ledger, ordinal, placement, commit order) |
| P-17 | `FissionHostPort`, `FissionOwnerRuns`, `FissionPtyCompletion`, `FissionPtyStatus`, `FissionSessionPort`, `FissionEventPort` | `Host/FissionPort.fs(i)` | fission | WIRED-OC. Declared in namespace `Wanxiangshu.Host.Ports` (drift **D-05**) but the file stays outside `Host/Ports/`, so gate ⑫ does not treat it as a neutral family. D-03 partly resolved: `AgentJournal` left the port (it is threaded into the launch call instead); the legacy `ISessionHostPort` / `IEventObservationPort` remain until P-01 / P-07 land. S3 (2026-10-22): the Fission lane now speaks its **own** names — `FissionSessionPort` and `FissionEventPort` — and the legacy Foundation types are reachable only through those two **transitional** aliases, which are the migration seam P-01 / P-07 will re-point. `FissionHostPort.Sessions` / `.Events` and the whole `Execution/Fission/Convergence` seam use the Fission-owned names; the aliases are transparent, so behaviour is unchanged. S4 (2026-10-22): the `fission` tool's **product policy** left the adapter — the prose vocabulary, the prose rendering and the office-entitlement predicate now live in the product lane (`Execution/Fission/ToolPolicy.fs(i)`, `FissionToolPolicy`); the adapter keeps only the wire mapping (rejection → semantic path) and the wire encoding. No port shape changed. Status: **on `master`** (`2680bc07d`, `0287a2349` — the port moved into the `Wanxiangshu.Host.Ports` namespace — `9894b1e0c`, and the S3 / S4 slices); `origin/wip/hni-fission` is **deleted**. |
| P-18 | `HostToolContext`, `ToolAdmission` (declared authority: `OfficeRole` \| `PrivateAttachment`); `ToolResultText` is a **module** (product prose), not a type | `Host/Tools.fs(i)` | tools | Was proposed as a redefinition of "P-04" (D-02). P-04 stays the registration port in `Host/Ports/Tools.fs`; P-05's call verdict is `ToolCallVerdict` (renamed to match the tools lane). **D-07: this file name collides with P-04's `Host/Ports/Tools.fs(i)` — rename before moving it under `Host/Ports/`.** Status: **not on `master`** — the file lives on `origin/wip/hni-tools` (branch still exists, measured 2026-10-21). |

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
4. **Use a namespace that adapters do not `open`**: `Wanxiangshu.Host` is `open`ed by **95 files** under
   `src/Wanxiangshu/` (measured at `31a5a2426`; mostly for `CanonicalJson`/`HostDigest`), so a type named e.g.
   `Digest` in that namespace shadows the real one at every one of those call sites. Use
   `Wanxiangshu.Host.Domain.<Lane>` (**D-05**). The neutral families use `Wanxiangshu.Host.Ports`, which had
   **zero** openers when it was created; the openers it has today were each added deliberately (the first four by
   `4403f579b`, the fission ones by the HNI-05 lane). The risk **D-05** guards against is therefore not "some file
   `open`s the namespace" but "a **pre-existing** namespace with 90+ openers": port names placed in
   `Wanxiangshu.Host` would shadow real types across every one of those call sites, whereas a namespace nothing
   opened before is safe to add to. **An `open` is not an implementation** — see the status column for whether a
   port is actually wired.
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
| fission | `Execution/Fission/OpenCode` | P-02 (+ P-01 read-only use), P-17 |
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
- 2026-10-21 eng-hni-fork (bookkeeping): corrected the lane-port status — P-13/P-14/P-15 **are on `master`**
  (`58660d4d9`, `2e9d5025d`, `6c6b8cfdf`, `a4222e3fb`, `e16e72c34`, slice B `aa43d3455`); `origin/wip/hni-fork` and
  `origin/wip/hni-fork-next` are deleted. P-16/P-17/P-18 are still branch-only (`origin/wip/hni-plugin`,
  `origin/wip/hni-fission`, `origin/wip/hni-tools` — all three branches still exist). Recorded that the fork lane has
  **no compile/test evidence** (owner's no-build-before-push rule): the delivery team must build master at
  `aa43d3455` or later. No id, name or port shape changed; next free id stays **P-19**.
- 2026-10-21 eng-hni-fission: P-17 `FissionHostPort` declared and wired to the OpenCode fission adapter (transitional: still carries Foundation `ISessionHostPort` / `IEventObservationPort`).
- 2026-10-21 eng-hni-fission: S1 — the fission tool-visibility decision moved out of the adapter folders into the product lane (`Execution/Fission/Visibility.fs(i)`, `FissionToolVisibility`); no port change (P-17 unchanged).
- 2026-10-22 eng-hni-fission: S3 — the fission lane got its **own** convergence vocabulary. `Host/FissionPort.fs(i)`
  now declares `FissionSessionPort` and `FissionEventPort` as the Fission lane's names for the session-lifecycle and
  terminal-observation seams; today they are **transitional** aliases of the legacy Foundation `ISessionHostPort` /
  `IEventObservationPort` and are the seam P-01 / P-07 will re-point. `FissionHostPort.Sessions` / `.Events` and the whole
  `Execution/Fission/Convergence` seam (`tryConverge`, `observeLaneTurn` and their private helpers) speak only the
  Fission-owned names, so the legacy Foundation names no longer appear in the fission product lane. The aliases are
  transparent: no behaviour changed, and `Host/FissionPort.fs(i)` moved **earlier** in `compile-order.txt` (right after
  `Foundation/HostSessionPort.fs`) because `Convergence.fs` now depends on it. P-01 / P-07 are unchanged, so the next
  free id stays **P-19**; `omp: n/a` (the file is not under `Host/Ports/`, so the generated TS mirror is unaffected).
  **No compile/test evidence** (owner's no-build-before-push rule) — the delivery team must build master and run the
  intra-participant-parallelism requirement suites.
- 2026-10-22 eng-hni-fission: S4 — the `fission` tool's **product policy** left the OpenCode adapter. The prose
  vocabulary (the semantic paths `tool/fission/description`, `…/too-few`, `…/invalid-origin`, `…/capacity`,
  `…/already-active`, `…/unavailable`), the prose rendering and the office-entitlement predicate now live in the
  product lane (`Execution/Fission/ToolPolicy.fs(i)`, `FissionToolPolicy`: `Path`, `describe`, `instructions`,
  `isAllowedRole`). The adapter (`Execution/Fission/OpenCode/Tool.fs`) keeps only the wire half: the rejection →
  semantic-path mapping and the TOML encoding of the rendered instructions, plus the language binding it reads
  from the host. The unused `tool/fission/started` literal was dropped with the move. No port shape changed, so
  P-01 / P-07 / P-17 are unchanged and the next free id stays **P-19**; `omp: n/a` (no port type).
  Gates: architecture OK, adapter-thinness 139 files / 33132 lines, product-reference 595 (baseline 689),
  without-port 137 (baseline 150) — the `Execution/Fission/OpenCode/Tool.fs` per-file product-reference count is
  unchanged at 5, provider-leak-gate OK, tool-referential-integrity OK (one `fission` ToolSpec owner site).
  **No compile/test evidence** (owner's no-build-before-push rule) — the delivery team must build master and run the
  intra-participant-parallelism requirement suites.
- 2026-10-21 eng-host-arch-goose (correction): the statement that `master` has "no `Host/Ports/` consumer — the
  neutral families are declared types only" was false. Measured at `31a5a2426`: **7** files outside the shard
  `open Wanxiangshu.Host.Ports` — `OpenCode/Host/SessionSnapshotPort.fs`, `OpenCode/Host/SessionSnapshotPort.fsi`,
  `OpenCode/Host/SessionsSurface.fs` and `OpenCode/Plugin/PluginRecoveryWiring.fs` (all four added by `4403f579b`),
  plus `Execution/Fission/Launch.fs`, `Execution/Fission/Launch.fsi` and `Execution/Fission/OpenCode/Tool.fs` from
  the HNI-05 lane. (A count of 9 comes from matching *any* `Host.Ports`
  reference, which also hits `Host/FissionPort.fs(i)` — those two files are themselves in the
  `Wanxiangshu.Host.Ports` namespace and contain no `open` of it.) An `open` is not an implementation: P-02..P-12
  still have no implementation anywhere in `src/`, while **P-01 is implemented** (`SdkSessionPort`/`HttpSessionPort`
  in `OpenCode/Host/SessionSnapshotPort.fs`, `interface ISessionPort with`) and P-17 is implemented by the OpenCode
  fission adapter — so their rows say `WIRED-OC` and the other rows do not. Also corrected: **P-17 is on `master`**
  (`2680bc07d`, `0287a2349`, `9894b1e0c`) and `origin/wip/hni-fission` is **deleted**; P-16 and P-18 remain
  branch-only on `origin/wip/hni-plugin` / `origin/wip/hni-tools` (both branches still exist). The checklist item 4
  "76 files" figure was replaced by the mechanism it stands for: `open Wanxiangshu.Host` matched **95** files at
  `31a5a2426`, while `Host.Ports` had **zero** openers at creation and every opener it has today was added
  deliberately by a named commit. Documentation only — no build, no compile, no test run; all numbers are static
  `git grep`/`git ls-tree` measurements at `31a5a2426`.
- 2026-10-21 eng-hni-fork (slice C): the join run's product decisions leave `Fork/OpenCode/JoinTool` for host-neutral
  `Execution/Delegation/Fork/JoinRun` (`JoinRun`: `JoinRoute`, `JoinLane`, `AgentIdentity`, `BatchWindow`, `decideRoute`,
  `appliesWaitBudget`, `normalizeInterruptReason`, `decideLane`, `resolveAgentName`, `resolveAgentRole`,
  `resolveTerminalLabel`, `windowOf`, `ptyTrackIds`, `mapOrchestratorMailboxError`, `DevOpsJoinTimeoutMs`). **No new port**:
  the rules consume only product-domain types (`Role`, `JoinItem`, `JoinInterruptReason`, `NonEmptyBatch`); the host
  capability they need is supplied by the adapter as one plain record of callbacks (`JoinRun.AgentIdentity`), so the
  neutral module holds no host capability. Next free id stays **P-19**. The adapter keeps only the wiring (journal/runtime
  identity lookup, mailbox wait, PTY release and buffering, wire rendering) and re-exports `JoinRecord` unchanged, so the
  requirements import path `dist/Execution/Delegation/Fork/OpenCode/JoinTool.js` is untouched. `omp: n/a` (no port type).
  **No compile/test evidence** (owner's no-build-before-push rule; the compile slot gave up with exit 75 twice) —
  the delivery team must build master and run the join requirement suites.
- 2026-10-22 eng-hni-fork (slice 2a): the OpenCode adapter's P-01 session port now implements **all seven verbs**
  (`Create`, `Parent`, `Children`, `Abort`, `Interrupt`, `Close`, `FindByTitle`) instead of the discovery-only
  skeleton. `OpenCode/Host/SessionSnapshotPort.fs`: `SdkSessionPort` and `HttpSessionPort` map the host payloads
  (SDK `session.create` / `session.get` / `session.children` / `session.abort` / `session.delete|close`; HTTP
  `POST /session`, `GET /session/{id}`, `GET /session/{id}/children`, `POST /session/{id}/abort`) onto the neutral
  port records — host rows become `SessionInfo {Id; Parent; Role; Title}`, the host `agent` field becomes
  `AgentRole`, and the host payload shape never leaves the adapter. The `Unsupported` stubs for
  `HostCapability.ChildSessions` / `HostCapability.Interrupt` are gone. `Interrupt` is documented as the host's
  single session-level stop action (the host exposes no attempt-scoped stop). `SdkSessionPort` / `HttpSessionPort`
  are now exposed in `SessionSnapshotPort.fsi`. **No new port** — P-01 (`Host/Ports/Sessions.fs(i)`) is unchanged, so
  the next free id stays **P-19**; `omp: n/a` (the family `.fsi` did not change, so the generated TS mirror is
  unaffected). The product still consumes the legacy `ISessionHostPort` / `ISessionSnapshotPort`; routing those call
  sites onto P-01 is the next slice. Gates: architecture OK, adapter-thinness 139 files / 33352 lines,
  product-reference 596 (baseline 689), without-port 137 (baseline 150), provider-leak-gate OK.
  **No compile/test evidence** (owner's no-build-before-push rule; the compile slot gave up with exit 75 because the
  machine was heavily loaded) — the delivery team must build master and run the session requirement suites.

- 2026-10-21 eng-hni-plugin-goose (HNI-08): P-16 pair guidance is WIRED-OC — the guideline fold moved out of
  `OpenCode/Host/PairProgramming` into the domain owner `Interaction/PairGuidance/Occurrences`; the OpenCode transform
  decodes the transcript and renders only. The ID is P-16 (D-01: P-13 is the fork lane's `CommandDeclaration`).

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

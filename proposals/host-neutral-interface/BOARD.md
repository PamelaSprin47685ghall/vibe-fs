# HNI board

Claim like the extraction kanban: edit the row on latest master (Owner, DOING), commit `board(HNI-NN): claim`,
land rebase + ff. Ratchets (architecture ⑧, ⑨, HNI-04 thinness) only go down.

| Card | Title | Lane | Prereq | Owner | State |
|---|---|---|---|---|---|
| HNI-01 | DESIGN + REGISTRY + BOARD skeleton | arch | — | eng-host-arch-goose | DONE |
| HNI-02 | Declare P-00 vocabulary + port `.fsi/.fs` files under `src/Wanxiangshu/Host/` (types only, no consumers **when this card was written**) | arch | 01 | eng-host-arch-goose | DONE |
| HNI-03 | omp 18.12.4 feasibility evidence + omp adapter skeleton type-checked against the ports with offline fixtures | arch | 01 | eng-host-arch-goose | DONE (evidence/omp-feasibility.md; O-1..O-5 decided in HNI-13)；2026-10-21 更新：仓主已指认 omp 18.12.4 为第二宿主并授权真实模型调用，canary 卡 CUT-36 转 DOING；本卡的骨架**仍是骨架**（无真实宿主绑定），端口族状态保持 DECLARED / SKELETON-OMP，不因 canary 授权而升级 |
| HNI-04 | Architecture gate: adapter thinness (product decisions in adapter folders; adapter files with no neutral port) + honest baseline | arch | 01 | eng-host-arch-goose | DONE (gate ⑩ 695 / 151, ⑪ 0, ⑫ 0, ⑬ 0) |
| HNI-05 | Fission: move product logic out of `Execution/Fission/OpenCode` behind P-02 | fission | 02 | (fission lane) | DOING |
| HNI-06 | Fork/children: `Execution/Delegation/Fork/OpenCode` + `Execution/*/OpenCode` behind P-01/P-03 (+ P-13..P-15) | fork | 02 | eng-hni-fork-goose | DOING — **landed**: 6 commits on `master` (`58660d4d9` ForkCommand, `2e9d5025d` HorizonRoster, `6c6b8cfdf` JoinWake, `a4222e3fb` SyncDelegate batching, `e16e72c34` gate ⑩ honesty, `aa43d3455` slice B `Fork/JoinRules`); P-13/P-14/P-15 (`Host/Domain/CommandDeclaration.fs(i)`, `Host/Domain/DelegationCaller.fs(i)`, `Host/Domain/DelegationCommand.fs(i)`) are on `master` — moved into the tier-B folder `Host/Domain/` on 2026-10-22; gate ⑩ 651/144 on that tip (baseline untouched); `Fork/OpenCode` now holds only `JoinTool.fs(i)`, `Tool.fs(i)`, `ToolSurface.fs(i)`. **Still open**: (a) JoinTool product rules (it keeps the wiring, the join product rules are not fully extracted); (b) P-01/P-03 wiring — no `Host/Ports/` consumer exists yet in the fork lane; (c) `Fork/OpenCode/ToolSurface.fs` harness split (harness still lives in the adapter). **NOT built/tested** — owner no-build-before-push rule; the delivery team must build master at `aa43d3455` or later. |
| HNI-07 | Tools: ToolRegistry/ToolRuntimeScope/Codec behind P-04/P-05 (+ Mission/Work, Repository tool bindings) | tools | 02 | eng-hni-tools-goose | DONE — landed on `master` as ``14119b211`, `6540a7f68`, `52518bc0a`, `97d524710`, `307d2867f`, `867638544`, `151b4372f`, `efbec4d04`, `ccc7c1273`, `a4fefc897``: the tool execute gate (`Execution/Tool/ExecuteGate.fs`), the per-tool declared admissions (`Execution/Tool/Admissions.fs`) and the name→admission table (`Execution/Tool/AdmissionTable.fs`) left the OpenCode adapter, `DeliveryAttemptLedger` moved to `Mission/Work`, and P-18's file was renamed `Host/Tools.fs(i)` → `Host/ToolContext.fs(i)` (D-07; gate ⑫ 0). Gate ⑩ `product-reference 585 (baseline 689) / without-port 134 (baseline 149)`. Remaining convergence (D-02/D-04/D-05) is HNI-14. |
| HNI-08 | Plugin host: Plugin/Host behind P-06..P-09, P-11, P-12; product = `HostPorts -> ProductHooks` | plugin-host | 02 | eng-hni-plugin-goose | DOING — **slice 2+3 landed**: P-00, P-02, P-09, P-10, P-11, P-12, P-19 are WIRED-OC (product: `Composition/Plugin/{Activation,CompactionGate,ConfigPolicy}.fs`; OpenCode: `OpenCode/Host/OpenCodeHostPorts.fs`, `OpenCode/Plugin/OpenCodeHostConfig.fs`, `PluginHooks`/`SpikePlugin` cutover). Still open in HNI-08: P-06 (`ITurnTransform` extraction from `PluginTransforms`/`ChatParamsHook`/`ProviderSystemTransform`) and P-07 (`ISignalSource` cutover) — **not build-verified**: the owner Fable compile exited 75 (build-slot queue timeout on an overloaded machine) and verification was done by static analysis instead |
| HNI-09 | Leftovers: Mission/Work, Repository, Sphinx V2 hosts, Context/Prefix, Interaction/Dispatch `OpenCode` folders | domains (this lane) | 02 | domain-cores | DOING — **landed so far** (all on `master`): `40b9967fd` (the three dispatch-correlation wire literals — `wanxiangshu_prompt_key` / `wanxiangshu_origin` / `wanxiangshu_logical_run` — moved to the neutral `Wanxiangshu.Foundation.PromptMetadataField`, one literal per wire field), `c27056ee6` (the js-* tool product decisions into the product module `Repository/Programming/Js/JsToolPolicy.fs(i)`), `500a47c5c` (**Context/Prefix**: `Context/Prefix/OpenCode/Wire.fs(i)` now takes the neutral **P-03** `ITranscriptPort option` instead of the legacy `ISessionSnapshotPort option`; signature-only, provider edit semantics untouched), `e97ec7b5e` (**Interaction/Dispatch**: `IDispatchSessionPort.SendPrompt` and `Interaction/Dispatch/Send.fs` now speak the neutral **P-02** `PromptRequest` + `DeliveryVerdict`/`LateDeliveryListener`; the legacy `OpenCodePromptOptions` survives only at the JS host boundary), plus earlier `052e1390d` (Mission relay core), `b87ee2e44` (Casebook tool contribution decisions → `Repository/Knowledge/Casebook/ToolPlan.fs(i)`) and `2702b0058` (Sphinx dispatch decision → `Sphinx.V2.Core.DispatchPolicy`). **Still in flight / open**: the Repository js-* adapter files (`Repository/Programming/Js/OpenCode/{ToolHost,ToolHostSurface,BookkeeperTool}.fs`) and the Casebook/Sphinx/Mission adapters still carry product references and are still in the `without-port` list; `Interaction/Dispatch/OpenCode/IngressCodec.fs` still carries one product reference and is still in `without-port` (cutover needs P-03 to carry the `synthetic` flag and the multi-source compaction judgement — out of scope for this lane); P-05 (`IToolGate`) has no HNI-09 consumer yet. **NOT BUILD-VERIFIED / NOT TEST-RUN** — verified by static gates only |
| HNI-10 | Retire host-shaped Foundation names (`OpenCodePromptOptions`, `OpenCodeChildOptions`, `OpenCodeChildInfo`, `OpencodeModel`, `IOpenCodePort`) via aliases then deletion | arch | 05–08 | — | DOING — **slice C landed (2026-10-22)**: the four Foundation type names were retired *in place* (no alias kept): `OpencodeModel` → `HostModel`, `OpenCodePromptOptions` → `SessionPromptOptions`, `OpenCodeChildOptions` → `ChildSessionOptions`, `OpenCodeChildInfo` → `ChildSessionInfo`; the alias `type SessionPromptOptions = OpenCodePromptOptions` is deleted and `SessionPromptOptions` is now the primary neutral name. The wire field names (`providerID` / `modelID` / `variant`) are byte-identical, so the Fable-emitted JS contract and the frozen manifest modules are untouched. **Still open**: the *types* themselves still live in Foundation (they are not yet `SessionSpec` / `SessionInfo` / `PromptRequest` / `ModelRoute`), and `IOpenCodePort` (12 files / 37 occurrences) stays in `OpenCode/Host/OpenCodeContract.fs(i)` until the P-01/P-02 consumers are cut over |
| HNI-11 | Drift review of lane port additions (names, shapes, HostError use, registry rows) | arch | 05–08 running | eng-host-arch-goose | DOING (continuous) — D-05/D-06/D-07 recorded in REGISTRY.md; gate ⑫ (port-family identity) added in `f214ece07`; **gate ⑬ (namespace-tier integrity) added 2026-10-22** — `Host/Ports/` declares `Wanxiangshu.Host.Ports`, `Host/Domain/` declares `Wanxiangshu.Host.Domain`, no file directly under `Host/` may claim either, and the tier-B seam's adapter references are ratcheted (`scripts/checks/domain-seam-baseline.json`); the tools lane's `Host/Tools.fs` must be renamed before it moves under `Host/Ports/` (D-07) |
| HNI-13 | omp open items O-1 (entry ids), O-2 (provider params encode), O-3 (Fable-built product bound to `activateOnOmp`), O-4 (`parentSession` may be a session file path, not an id), O-5 (`AgentSession.sessionId` is the *provider* id, not the persisted one — the neutral `SessionId` is `sessionManager.getSessionId()`) | arch | 08 | cut36-omp-2 | DECIDED — rules O-1..O-5 in evidence/omp-feasibility.md; implementation owned by cut36-omp-2；2026-10-22 进展：O-1（条目 id）、O-4（不可解析的父会话 → NotFound）已落地；O-3 入口门禁在推进（Fable 产物与 activateOnOmp 的真实绑定尚未成立）；O-2 因宿主无可达前提而阻塞（ProviderParams 能力不广告）；真实 smoke 尚未通过，仍不宣称已支持第二宿主 |
| HNI-14 | Converge lane domain ports (D-02, D-03, D-04 in REGISTRY.md): split P-14/P-15/P-17 into neutral HostPorts + Composition inputs; merge P-18 `HostToolContext` with `ToolInvocation`; move `ToolResultText` to a product module | arch + owning lanes | 05–08 | — | DECIDED (evidence/hni14-convergence.md) — steps S1..S5 owned by the fork/session, fission, tools and arch lanes; shape convergence not implemented; step (a) namespace relocation landed in `51aadb02f`. **D-05 resolved** (2026-10-22: P-13/P-14/P-15/P-16 now live in `Host/Domain/` under `Wanxiangshu.Host.Domain`), **D-06 partly resolved** (each seam file carries its `HNI P-xx` marker). **Remaining**: D-02/D-03/D-04 (the shape convergence above) and P-17's **location** — `Host/FissionPort.fs(i)` already declares the safe `Wanxiangshu.Host.Ports` namespace but still sits directly under `Host/`, so it is the one exemption of gate ⑬'s location rule until the fission lane moves it to `Host/Ports/` |
| HNI-12 | Final accounting: every REGISTRY row WIRED-OC + WIRED-OMP or explicitly RETAINED with reason; gate counts | arch | all | — | BACKLOG |


HNI-05 progress (eng-hni-fission, 2026-10-22): S0 landed the transitional P-17 port. S1 done: the fission tool-visibility decision is product-owned in `Execution/Fission/Visibility.fs(i)` (`FissionToolVisibility`); the adapters keep only the wire write. S2 done: the fission canaries moved to the product lane (`Execution/Fission/Canaries.fs(i)`); `OpenCode/Host/FissionHostSurface.fs` shrank 251 → 46 lines. S3 done: the Fission lane speaks its own convergence vocabulary (`FissionSessionPort` / `FissionEventPort` in `Host/FissionPort.fs(i)`). S4 done: the `fission` tool's product policy (prose vocabulary, prose rendering, office entitlement) is product-owned in `Execution/Fission/ToolPolicy.fs(i)` (`FissionToolPolicy`); the adapter keeps only the wire mapping and encoding. S0–S4 are on `master`.

P-03 fact (arch lane, 2026-10-22, slice S3): P-03 now has an OpenCode implementation —
`OpenCode/Host/TranscriptPort.fs(i)` implements `ITranscriptPort` as a projection over the existing
`ISessionSnapshotPort` plumbing, and the neutral port family `Host/Ports/Transcript.fs(i)` is unchanged. This is
recorded as a fact only: it touches the HNI-08 (plugin-host) and HNI-09 (leftovers) boards' transcript surface,
but it claims no card and changes no card state. The legacy `ISessionSnapshotPort` / `SessionMessage` contract
stays live and no consumer was cut over; routing them onto P-03 is the next slice.

P-01 progress (eng-hni-host, 2026-10-22): the OpenCode adapter's `ISessionPort` implementation is WIRED-OC for all
seven verbs (`Create`, `Parent`, `Children`, `Abort`, `Interrupt`, `Close`, `FindByTitle`) in both `SdkSessionPort` and
`HttpSessionPort` (`OpenCode/Host/SessionSnapshotPort.fs`). Every failure is mapped into the neutral error
vocabulary (`HostError.NotFound` / `Refused` / `Transport`); no `Result<_, string>` channel and no raw host payload
escapes the adapter. Two wire defects were corrected: the HTTP `Close` called `POST /session/{id}/abort` and now
calls `DELETE /session/{id}`, and the SDK `Close` now sends the route payload the SDK expects. `Interrupt` is not a
capability downgrade — the host API generation this adapter speaks (v1) has one stop action per session
(`POST /session/{id}/abort`) and no attempt-scoped interrupt route; the host's `session.interrupt` belongs to the v2
API generation, which this adapter does not speak, so the attempt-scoped stop maps onto that same `session.abort`
verb the product already uses. No verb is left `Unsupported`. `Host/Ports/*` is unchanged (gate ⑪ intact, `omp: n/a`),
so the port family is unchanged and the next free port id is **P-20** (P-19 was declared as `HostConfig` by `93a51ca15`).

Host-lifecycle leftover audit (eng-hni-host, 2026-10-22): the adapter is complete (above), but product lifecycle
consumers remain on legacy `ISessionHostPort` / `ChildSessionOptions` / `ChildSessionInfo` (renamed in place by HNI-10
slice C) across four clusters:
(1) Fork (`Execution/Delegation/Fork/Host/Agent.fs`, `HostForkRuntime`), (2) SyncDelegate (`Execution/Delegation/SyncDelegate/Runtime.fs`),
(3) Advisor (`Execution/Delegation/Advisor/Seat.fs`), (4) OpenCode host wiring (`OpenCode/Host/Sessions.fs`, `PluginHost.fs`).
These consumers are precisely documented as left (in `保姆级交接与展望.md` and `REGISTRY.md`) rather than cut over in an
unverified cross-lane sweep: `AdvisorSeat` requires terminal event subscription (`SubscribeTerminal`) which `ISessionPort`
does not provide, `InjectedSessionPort` still manages family roots and process-local ancestry, and `ChildSessionOptions`/
`ChildSessionInfo` have 36 live references across 17 files, which prevents retirement of the *types* at this
stage (the *names* were retired in place by HNI-10 slice C).

HNI-10 slice C fact (arch lane, 2026-10-22): the *names* were retired without touching the wire. The four
host-shaped Foundation type names became host-neutral names in place —
`OpencodeModel` → `HostModel` (`Foundation/HostModel.fs(i)`),
`OpenCodePromptOptions` → `SessionPromptOptions`, `OpenCodeChildOptions` → `ChildSessionOptions` and
`OpenCodeChildInfo` → `ChildSessionInfo` (`Foundation/HostSendContract.fs(i)`); the old alias
`type SessionPromptOptions = OpenCodePromptOptions` in `Foundation/HostSessionPort.fs(i)` is deleted, so
`SessionPromptOptions` is the primary name and the durable-events requirement probe (which already names
`SessionPromptOptions` as the `ISessionHostPort.SendPrompt` third parameter type) still matches. The record
fields of `HostModel` (`providerID` / `modelID` / `variant`) are byte-identical, so the Fable-emitted JS
property names, the frozen `ModelRoutingSurface` / `ProviderProjectionSurface` manifest modules and the JS
requirement suites are unaffected. `ModelRouting.toOpenCodeModel` was renamed to `toHostModel` (the F# name
erases in JS). The *types* are still Foundation-owned and still adapter-shaped: retiring them onto
`SessionSpec` / `SessionInfo` (P-01), `PromptRequest` (P-02) and `ModelRoute` (P-08) remains blocked on the
consumer cutovers and on the JS property-name lock. `IOpenCodePort` stays in
`OpenCode/Host/OpenCodeContract.fs(i)` for the same reason.

P-03 consumer cutover 1 (eng-hni-host, 2026-10-22): the session recovery host now reads the transcript through the
neutral port. `OpenCode/Host/SessionRecoveryHost.fs(i)`: the constructor argument `snapshot: ISessionSnapshotPort`
became `transcript: ITranscriptPort`; `OpenCode/Host/SessionRecoveryHostSurface.fs` builds an `ITranscriptPort` stub at
both construction sites, and `OpenCode/Host/HostSignalBootstrap.fs` adapts its existing snapshot port through
`TranscriptPort.HostTranscriptPort` (that file is shared with the signal lane; only the constructor argument
changed there). The legacy `ISessionSnapshotPort` / `SessionMessage` contract stays live — the adapter wraps it and
the remaining consumers (turn reconcile, prefix wire, delegation advisor, tools, dispatch recovery) are still to be
routed onto P-03. `Host/Ports/*` is unchanged, so the next free port id stays **P-20**. Gates: architecture OK,
adapter-thinness 139 files / 33000 lines, product-reference 595 (baseline 689), without-port 134 (baseline 149),
fsharp-control-pyramid 2 findings (both pre-existing). **NOT BUILD-VERIFIED, NOT TEST-RUN** — the delivery team must
build master and run the recovery requirement suites.
P-03 consumer cutover 2 (eng-hni-host, 2026-10-22): the dispatch recovery path now reads the transcript through the
neutral port. `Interaction/Dispatch/Recovery.fs(i)` (`PromptRecovery.reconcile` now takes `ITranscriptPort option`) and
`Interaction/Dispatch/RecoverySurface.fs` (the private snapshot stub became an `ITranscriptPort` stub). The claim law
is unchanged; a host role outside the neutral vocabulary is carried as `system` so the 50-message recovery tail window
is preserved. The legacy `ISessionSnapshotPort` / `SessionMessage` contract stays live. Fork recovery (`ChildRecoveryWorkflow.Ports` /
`Fork/Host/Restart.fs`) was evaluated for cutover but deferred: `HostForkRuntime` and `ToolRuntimeScope` fan out into 24 tool files
(126 call sites across `JoinTool`, `DeliverTool`, `ReviewBackendManager`), and tools like `ReviewBackendManager` rely on snapshot-specific
`SessionToolPart` / `ToolParts` / `locateToolCall` absent from neutral `TranscriptPart.ToolUse`. Per HNI policy against broad multi-lane
fanout and lossy reverse projection, fork recovery remains on `ISessionSnapshotPort` until downstream tools complete domain extraction.
Full leftover inventory (44 files) documented in root `保姆级交接与展望.md`. Remaining consumers (turn reconcile, prefix wire, delegation
advisor, tools, fork host restart) are still to be routed onto P-03.
`Host/Ports/*` is unchanged, so the next free port id stays **P-20**. Gates: architecture OK, adapter-thinness 139
files / 33004 lines, product-reference 589 (baseline 689), without-port 134 (baseline 149), fsharp-control-pyramid 2
findings (both pre-existing). **NOT BUILD-VERIFIED, NOT TEST-RUN** — the delivery team must build master and run the
dispatch-protocol / effect-accounting recovery requirement suites.

P-08 fact (arch lane, 2026-10-22, slice P-08): P-08 now has an OpenCode implementation —
`OpenCode/Host/ModelCatalogPort.fs(i)` implements `IModelCatalog`, and the neutral port family
`Host/Ports/ModelRouting.fs(i)` is unchanged. `Available` reads the host catalog over
`client.config.providers` (`GET /config/providers`); the host catalog carries no variant field, so every
enumerated route has `Variant = None`. `Apply` is `HostError.Unsupported HostCapability.ProviderParams`:
the host binds a model per prompt, never to a session. This is recorded as a fact only: it touches the
HNI-08 (plugin-host) board's model-routing surface, but it claims no card and changes no card state. The
`HostModel` (renamed from `OpencodeModel` by HNI-10 slice C) and the `ModelRoutingSurface` host calls stay live (retirement is
HNI-10) and no consumer was cut over; routing them onto P-08 is the next slice.

P-08 wiring fact (arch lane, 2026-10-22, slice C): the neutral model catalog is now really wired on OpenCode.
`OpenCode/Host/OpenCodeHostPorts.fs` builds the catalog with `ModelCatalogPort.create boot.WorkspaceDirectory
boot.Input`; when the host input names a catalog source the adapter claims `HostCapability.ModelCatalog` in
`HostCapabilities.Supports`, and when it does not the capability stays unclaimed and the declared
`UnsupportedModelCatalog` fallback answers `HostError.Unsupported HostCapability.ModelCatalog` — never a fake
empty catalog that would read as "this host has no models". The Foundation type `HostModel` still lives in
`Foundation/HostModel.fs(i)` and is still adapter-shaped; moving it onto `ModelRoute` (P-08) is blocked by the
Fable JS property-name lock (`providerID` / `modelID` / `variant` vs `Provider` / `Model` / `Variant`) and by the
shard graph (`HostModel` lives in the leaf shard `host-boundary.host-opencode-types`, `ModelRoute` in
`host-boundary.host-neutral-ports`; 11 of 15 consuming shards do not reference `host-neutral-ports`).

P-12 fact (arch lane, 2026-10-22, slice P-12): P-12 now has an OpenCode implementation —
`OpenCode/Host/DiagnosticsPort.fs(i)` implements `IDiagnosticSink` over the host's own log verb
`client.app.log` (`POST /log`, SDK body `{ service, level, message, extra }`); the neutral port family
`Host/Ports/Diagnostics.fs(i)` is unchanged. The neutral `Emit` returns `unit` and carries no severity,
so every diagnostic is written at level `info` with the operation as the message and the fields as
`extra`; the write is fired and not awaited, and a failed log write is dropped rather than thrown into
product code (a diagnostic must not break a turn). Fact only: it touches the HNI-08 (plugin-host)
board's diagnostics surface, but it claims no card and changes no card state. The product's existing
diagnostic path is `Foundation/Diagnostic.fs` (`Diagnostic.emit`, a product diagnostic behind the CTX-014
whitelist and the `WANXIANGSHU_DIAG=1` gate), not the host verb. **Cutover (plugin-host lane, 2026-10-22,
slice A)**: `PluginBoot.create` now installs the `IDiagnosticSink` into `Foundation/Diagnostic.fs`
(`Diagnostic.installSink` / `clearSink`) from `DiagnosticsPort.create input`, so every `Diagnostic.emit`
call site routes redacted, CTX-014-validated records to `IDiagnosticSink.Emit` under `WANXIANGSHU_DIAG=1` in
addition to stderr. `OpenCodeHostPorts.create` takes the real `DiagnosticsPort.create boot.Input` and falls
back to `DiagnosticSinkPort` only when the host exposes no log verb — in that case boot cleared the sink,
so there is no loop. `Diagnostic.fatal` is unchanged (stderr + process termination).

P-10 fact (arch lane, 2026-10-22, slice P-10): P-10 now has an OpenCode implementation —
`OpenCode/Host/PathsPort.fs(i)` implements `IHostPaths`, and the neutral port family
`Host/Ports/Storage.fs(i)` is unchanged. `StateRoot` is the product's existing durable-state root
`RuntimePath.stateRoot` (`<primary worktree>/.Herdchestra`, `Persistence/Journal/RuntimePath.fs:86-118`) —
not a new directory and not host storage; because that resolution is async while the port is a plain
property, the composition root resolves it once and hands the adapter a memoized thunk. `ProjectRoot` is
the plugin's `input.directory`; `ProjectId` is the host-supplied `input.project.id` (SDK `Project.id`) with
the directory as the stable fallback, since no product code keys on a project id today. **Cutover (same
slice, 2026-10-22)**: `PathsPort` is now the *single authority* for the host's workspace field —
`OpenCode/Host/PathsPort.fs` is the only file under `src/` that reads `input.directory` or
`input.project`. `PluginBoot.create` resolves the paths once (`Boot.Paths : IHostPaths option`) and
derives `Boot.WorkspaceDirectory` from them (`PathsPort.projectRootOf`); the workspace directory is then
threaded as a parameter into `PluginHost` (`createJournal` / `createHost`), `OpenCodePortAdapter.create`,
`SessionSnapshotPort.create` / `createSessionPort` and `ModelCatalogPort.create` instead of being
re-read from the raw input. The duplicate readers are deleted: `PluginHost.workspaceDirectory` /
`nonBlankDirectory`, `OpenCodePort.tryWorkDirectory` / `tryNonBlankDirectory`,
`SessionSnapshotPort.directoryOption` / `nonBlankDirectory`, `ModelCatalogPort.directoryOption`, and
`OpenCodeHostPorts.projectIdOf` / `HostPathsPort` (the latter two replaced by `boot.Paths` plus the
`PathsPort.projectIdOf` helper, which also keeps the flat `projectID` / `projectId` spellings). Fact only:
it touches the HNI arch board's persistence-touchpoint surface, but it claims no card and changes no card
state.

integ-027-2 fact (2026-10-22): EMR-010 model-routing lease resolution landed (`a38ef7770`, keying `admissionInFlight`
by `(ChatExecutionKey * ChatAdmissionPurpose)`). Installed-Host canary gap diagnosed as lifecycle fence/abortion
mismatch (uncompleted join); canary remains unresolved. No HNI port added or changed; claims no card; next free
port id stays **P-20**; omp mirror n/a.

vs-006 fact (control-pyramid slice, 2026-10-22): the last two `fsharp-control-pyramid` findings in the OpenCode
session adapter are gone — `src/Wanxiangshu/OpenCode/Host/SessionSnapshotPort.fs` `sessionInfoOfRow` and
`parentOfPayload` now name their decision boundary instead of nesting a `match` under an `if` (landed as
`a46af54fda09a53d420671f86d8fc74e5d3f9fdf`, range `a2d2787cb..a46af54fd`, 1 file, +10/-14). Semantics are
unchanged (same `None` / `Error` cases, same `"Missing session response"` string, same field values, same
laziness of the `parentID` / `agent` / `title` host reads); all four touched functions keep their names and
arities. The pyramid baseline JSON was **not** touched (`{"version":1,"files":{}}`), no allowlist, no threshold
change, no scanner change, no helper relocation. Gates on the pushed tree: `fsharp-control-pyramid.mjs` →
`clean (0 nested decisions)` exit 0, `architecture.mjs` → `architecture: OK` exit 0.
**No HNI port added or changed** (P-01 / P-03 untouched, no port id consumed, no consumer cut over), so the
port-id bookkeeping on master is unchanged by this slice and the generated TS mirror is unaffected
(`omp: n/a`). **No compile or runtime F# proof** (no `build.mjs` / `dotnet build` / `build-slot` was run; the
owner permits a static-only landing and requires `build-slot` for any compile): whether `Option.map AgentRole`
type-checks, whether `|> Ok` infers `Result<SessionId option, HostError>` against the annotation, and whether
the edited offside region compiles are all unconfirmed — **the delivery team must build master and type-check
this file** (shard `…host-boundary.opencode-host-sessionquiescencegate`). Fact only: this claims no HNI card and
changes no card state. The same lane also produced the precise diagnosis of the still-RED
`requirements/verification-system` 006 suite (root cause: the supervisor's verdict-silence window is armed
before the work exists; the failure set is timing dependent — 46/59 pass 13 fail in one capture, 43/59 pass
16 fail in another, 41/59 pass 18 fail in a third; **no assertion, budget or product watchdog was changed**;
no genuine verdict was lost in the direct-inner and relaxed-window control runs, though that is **not globally
proved across uninstrumented runs**; a relaxed 30000 ms window is a diagnostic instrument only and is **not** an
acceptance configuration) — written up in `evidence/vs-006-root-cause.md`.


Lane worker ids: fill the Owner column when you claim (lanes started 06:05 UTC: fission, fork, tools, plugin-host).

## CUT-36（第二宿主真实 canary）当前口径（2026-10-21）

- **状态**：`DOING`。仓主已指认 **omp 18.12.4**（锁定版本）为第二宿主，并授权 canary 所需的真实模型调用。
- **本板不变的部分**：端口族仍是 `DECLARED` + `SKELETON-OMP`（`ports.ts` 为机械生成镜像，`mirror-check.mjs` 逐字节比对；适配器只做到类型检查 + 离线 fixture）。**不宣称已支持第二宿主，也不宣称 smoke 已通过。**

## Gate ⑩/⑪/⑫/⑬ (HNI-04, HNI-11) — `scripts/checks/adapter-thinness.mjs`, run inside `architecture.mjs`

- ⑩a product-reference: distinct product-domain namespaces (`Wanxiangshu.<Execution|Interaction|Mission|Context|…>`, not
  `*.OpenCode`) referenced per adapter file (`OpenCode/**`, `**/OpenCode/**`, future `Omp/**`). Start **695** over 151 files.
- ⑩b without-port: adapter files that use no host-neutral port type. Start **151 / 151** (no adapter is wired yet).
- Totals only go down; a baselined file may not grow; moves/splits are free while totals hold. Improvements don't fail;
  the arch lane tightens with `node scripts/checks/adapter-thinness.mjs --write-baseline`.
- ⑪ port-purity (strict, 0): `Host/Ports/<family>.fs` may reference only Foundation/Host/System and no host vocabulary.
- ⑫ port-family integrity (strict, 0): every family under `Host/Ports/` declares **exactly one** reserved id (`HNI P-xx` in its
  header); no two families claim the same id; no file directly under `Host/` may take the base name of a neutral family
  (the **D-07** check). The scope is discovered from the sources, so a family another lane lands is covered without
  editing the gate.
- ⑬ namespace-tier integrity (strict, 0, added 2026-10-22): `Host/Ports/` is **tier A** and declares exactly
  `Wanxiangshu.Host.Ports`; `Host/Domain/` is **tier B** and declares exactly `Wanxiangshu.Host.Domain`; no file
  directly under `Host/` may claim either namespace (the shadowing `Wanxiangshu.Host` was drift **D-05**; the
  marker is drift **D-06**). ⑬ also **supersedes** the old statement that ⑫ deliberately does *not* gate the
  namespace: the namespace is now a rule, because the **tier** is. The one exemption is P-17's
  `Host/FissionPort.fs(i)`, whose namespace is already safe and whose *location* is still open (HNI-14).
  ⑬'s second half is a **ratchet** over the adapter files that reference the tier-B seam
  (`scripts/checks/domain-seam-baseline.json`, 4 files): the set may only shrink. It records existing debt; it does
  not forbid the seam from being used.

HNI-08 progress (plugin-host lane, 2026-10-22, slice 2+3): the product side and the OpenCode side now meet. The
product is host-neutral: `Composition/Plugin/Activation.fs` turns `HostPorts` into `ProductHooks`
(`ProductHostPlugin.Activate`), `Composition/Plugin/CompactionGate.fs` implements `ICompactionGate` (`OnCompacting`
contributes nothing; `AutoContinue` denies while `HostCompactionPolicy.autoContinueEnabled` is false) and
`Composition/Plugin/ConfigPolicy.fs` establishes the required compaction settings over `IHostConfigPort`. The
OpenCode adapter builds the `HostPorts` in `OpenCode/Host/OpenCodeHostPorts.fs` (prompt/session/transcript/paths/
diagnostics implemented; models are implemented as of slice C through `ModelCatalogPort`, and
`HostCapability.ModelCatalog` is claimed whenever the host input names a catalog source; signals/tools/agents
answer `HostError.Unsupported` for a capability the host does
not hold — absence is declared, never faked), implements `IHostConfigPort` in `OpenCode/Plugin/OpenCodeHostConfig.fs`
(`LiveHostConfig` over the live host config object), and `OpenCode/Plugin/PluginHooks.fs` now routes the `config`
hook through `ConfigPolicy` and the two compaction hooks through the neutral `ICompactionGate`;
`OpenCode/Plugin/SpikePlugin.fs` activates the product plugin at plugin startup. `HostCapability` gained
`ToolRegistration`, `ModelCatalog`, `SignalSubscription` and `AgentCatalog`, and `IAgentCatalog.Roles` is typed
`HostResult<AgentRoleSpec list>`. Gates: architecture OK; adapter-thinness 141 files / 33320 lines,
product-reference 590 (baseline 689), without-port 132 (baseline 149); the TS mirror is byte-identical to the
generated mirror; `fsharp-control-pyramid` reports 0 findings in every file this lane touched (the 2 repo-wide
findings are pre-existing debt in `OpenCode/Host/SessionSnapshotPort.fs`, red on `origin/master` too).
**Remaining inside HNI-08**: P-06 (`ITurnTransform` extraction from `PluginTransforms`/`ChatParamsHook`/
`ProviderSystemTransform`) and P-07 (`ISignalSource` cutover). P-04/P-05 belong to the tools lane, P-08 to the
model-catalog lane.

HNI-09 progress (domain-cores lane, 2026-10-22): five slices are on `master`. (1) The three dispatch-correlation
wire literals (`wanxiangshu_prompt_key`, `wanxiangshu_origin`, `wanxiangshu_logical_run`) now live in one neutral
place — `Foundation/PromptMetadataField.fs(i)` — and `Interaction/Dispatch/MetadataCodec.fs(i)` no longer
re-exports them, so a wire field never carries two literals; `Interaction/Dispatch/OpenCode/IngressCodec.fs` and
`OpenCode/Codec/ProviderWireDecode.fs` read them through `Wanxiangshu.Foundation` (their product-reference counts
dropped 2 → 1 each). (2) The js-* tool's product decisions (role admission, execution root, the localized
`program`/refusal/view-declaration prose, the flat view declaration and its validation/commit) moved into the
product module `Repository/Programming/Js/JsToolPolicy.fs(i)`. (3) The Context prefix wire path
(`Context/Prefix/OpenCode/Wire.fs(i)`, `XWire.applyTransform` / `applyOrdinaryTransform`) now takes the neutral
**P-03** `ITranscriptPort option` instead of the legacy `ISessionSnapshotPort option`; the snapshot argument was
already unused inside `applyOrdinaryTransform`, so this is a signature-only change and the provider edit semantics
(`suppressHostMessagesByIds`, rendered-message insertion) are byte-for-byte unchanged. (4) The dispatch send path
(`Interaction/Dispatch/Send.fs`, `Interaction/Dispatch/DispatchSessionPort.fs(i)`) now speaks the neutral **P-02**
shape: `IDispatchSessionPort.SendPrompt` takes `(sessionId, PromptRequest, metadata side channel,
LateDeliveryListener)`, the options *record* shape survives only at the JS-facing host boundary (the type
is now `SessionPromptOptions`), where
`DispatchSessionPort.toHostOptions` translates back, and the neutral `DeliveryVerdict` crosses the port while the
legacy `DetachedSendVerdict` stays inside the product. (5) Earlier slices on `master`: the Mission relay-retirement
decision moved into `Mission/Work` core (`052e1390d`), the Casebook tool contribution decisions moved behind
`Repository/Knowledge/Casebook/ToolPlan.fs(i)` (`b87ee2e44`), and the Sphinx dispatch decision moved to
`Sphinx.V2.Core.DispatchPolicy` (`2702b0058`). Gates on `ab82deee3`: `architecture: OK`;
`adapter-thinness: 146 adapter files, 34608 lines; product-reference 586 (baseline 689); without-port 129
(baseline 149)` — the baseline JSON was **not** rewritten (the ratchet tightening is the arch lane's call).
**Still open in HNI-09**: the Repository js-* adapters (`Repository/Programming/Js/OpenCode/{ToolHost,ToolHostSurface,
BookkeeperTool}.fs`) and the Casebook / Sphinx / Mission adapters still carry product references and are still in the
`without-port` list; `Interaction/Dispatch/OpenCode/IngressCodec.fs` still carries one product reference and is
still in `without-port` — cutting it over needs P-03 to carry the `synthetic` flag and the multi-source compaction
judgement, i.e. a change under `Host/Ports/` that this lane must not touch; and P-05 (`IToolGate`) still has no
HNI-09 consumer. **NOT BUILD-VERIFIED, NOT TEST-RUN** — the delivery team must build master and run the
host-boundary / dispatch-protocol / repository-programming requirement suites.

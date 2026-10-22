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
| HNI-07 | Tools: ToolRegistry/ToolRuntimeScope/Codec behind P-04/P-05 | tools | 02 | (tools lane) | DOING |
| HNI-08 | Plugin host: Plugin/Host behind P-06..P-09, P-11, P-12; product = `HostPorts -> ProductHooks` | plugin-host | 02 | eng-hni-plugin-goose | DOING — P-19 `HostConfig` declared (host-neutral-ports fsproj + compile-order + omp mirror); P-11 `HostPorts.Config` added and `ProductHooks.ToolGate`/`.Turn`/`.OnSignal` optional; product activation and the OpenCode `HostPorts` builder are the remaining work (not build-verified) |
| HNI-09 | Leftovers: Mission/Work, Repository, Sphinx V2 hosts, Context/Prefix, Interaction/Dispatch `OpenCode` folders | domains (this lane) | 02 | domain-cores | DOING (Mission relay core landed 052e1390d; Repository/Context/Sphinx/Interaction in progress) |
| HNI-10 | Retire host-shaped Foundation names (`OpenCodePromptOptions`, `OpenCodeChildOptions`, `OpenCodeChildInfo`, `OpencodeModel`, `IOpenCodePort`) via aliases then deletion | arch | 05–08 | — | BACKLOG |
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
consumers remain on legacy `ISessionHostPort` / `OpenCodeChildOptions` / `OpenCodeChildInfo` across four clusters:
(1) Fork (`Execution/Delegation/Fork/Host/Agent.fs`, `HostForkRuntime`), (2) SyncDelegate (`Execution/Delegation/SyncDelegate/Runtime.fs`),
(3) Advisor (`Execution/Delegation/Advisor/Seat.fs`), (4) OpenCode host wiring (`OpenCode/Host/Sessions.fs`, `PluginHost.fs`).
These consumers are precisely documented as left (in `保姆级交接与展望.md` and `REGISTRY.md`) rather than cut over in an
unverified cross-lane sweep: `AdvisorSeat` requires terminal event subscription (`SubscribeTerminal`) which `ISessionPort`
does not provide, `InjectedSessionPort` still manages family roots and process-local ancestry, and `OpenCodeChildOptions`/
`OpenCodeChildInfo` have 36 live references across 17 files, which prevents retirement at this stage.

P-03 consumer cutover 1 (eng-hni-host, 2026-10-22): the session recovery host now reads the transcript through the
neutral port. `OpenCode/Host/SessionRecoveryHost.fs(i)`: the constructor argument `snapshot: ISessionSnapshotPort`
became `transcript: ITranscriptPort`; `OpenCode/Host/SessionRecoveryHostSurface.fs` builds an `ITranscriptPort` stub at
both construction sites, and `OpenCode/Host/HostSignalBootstrap.fs` adapts its existing snapshot port through
`TranscriptPort.HostTranscriptPort` (that file is shared with the signal lane; only the constructor argument
changed there). The legacy `ISessionSnapshotPort` / `SessionMessage` contract stays live — the adapter wraps it and
the remaining consumers (turn reconcile, prefix wire, delegation advisor, tools, dispatch recovery) are still to be
routed onto P-03. `Host/Ports/*` is unchanged, so the next free port id stays **P-19**. Gates: architecture OK,
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
`Host/Ports/*` is unchanged, so the next free port id stays **P-19**. Gates: architecture OK, adapter-thinness 139
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
legacy `OpencodeModel` and the `ModelRoutingSurface` host calls stay live (retirement is HNI-10) and no
consumer was cut over; routing them onto P-08 is the next slice.

P-12 fact (arch lane, 2026-10-22, slice P-12): P-12 now has an OpenCode implementation —
`OpenCode/Host/DiagnosticsPort.fs(i)` implements `IDiagnosticSink` over the host's own log verb
`client.app.log` (`POST /log`, SDK body `{ service, level, message, extra }`); the neutral port family
`Host/Ports/Diagnostics.fs(i)` is unchanged. The neutral `Emit` returns `unit` and carries no severity,
so every diagnostic is written at level `info` with the operation as the message and the fields as
`extra`; the write is fired and not awaited, and a failed log write is dropped rather than thrown into
product code (a diagnostic must not break a turn). Fact only: it touches the HNI-08 (plugin-host)
board's diagnostics surface, but it claims no card and changes no card state. The product's existing
diagnostic path is `Foundation/Diagnostic.fs` (`Diagnostic.emit`, a product diagnostic behind the CTX-014
whitelist and the `WANXIANGSHU_DIAG=1` gate), not the host verb; those call sites stay live and no

P-10 fact (arch lane, 2026-10-22, slice P-10): P-10 now has an OpenCode implementation —
`OpenCode/Host/PathsPort.fs(i)` implements `IHostPaths`, and the neutral port family
`Host/Ports/Storage.fs(i)` is unchanged. `StateRoot` is the product's existing durable-state root
`RuntimePath.stateRoot` (`<primary worktree>/.Herdchestra`, `Persistence/Journal/RuntimePath.fs:86-118`) —
not a new directory and not host storage; because that resolution is async while the port is a plain
property, the composition root resolves it once and hands the adapter a memoized thunk. `ProjectRoot` is
the plugin's `input.directory`; `ProjectId` is the host-supplied `input.project.id` (SDK `Project.id`) with
the directory as the stable fallback, since no product code keys on a project id today. Fact only: it
touches the HNI arch board's persistence-touchpoint surface, but it claims no card and changes no card
state; the live ad-hoc `input.directory` reads (`PluginHost.fs:41-51`, `OpenCodePort.fs:105-120`,
`SessionSnapshotPort.fs:340-346`, `PluginBoot.fs:64,107`) stay and no consumer was cut over onto P-10.

integ-027-2 fact (2026-10-22): EMR-010 model-routing lease resolution landed (`a38ef7770`, keying `admissionInFlight`
by `(ChatExecutionKey * ChatAdmissionPurpose)`). Installed-Host canary gap diagnosed as lifecycle fence/abortion
mismatch (uncompleted join); canary remains unresolved. No HNI port added or changed; claims no card; next free
port id stays **P-19**; omp mirror n/a.

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

HNI-08 progress (plugin-host lane, 2026-10-22): the neutral `HostPorts`/`ProductHooks` shapes are now complete enough to implement (P-19 `HostConfig` declared, `ToolGate`/`Turn`/`OnSignal` optional, absence declared not shimmed). Still open: `Composition/Plugin` activation (`HostPorts -> ProductHooks`), the OpenCode `HostPorts` builder, and the `PluginHooks` config/compaction cutover. No consumer of `HostPorts` exists yet.

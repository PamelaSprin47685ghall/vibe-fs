# HNI port registry

Single source of truth for host-neutral ports. **Rebase and read this file before adding or changing a port.**
Add/modify the row in the same commit as the code. Names and shapes follow [DESIGN.md](DESIGN.md) §1–§2.
Status: PLANNED (designed, no code) · DECLARED (`.fsi` exists in `src/Wanxiangshu/Host/`) · WIRED-OC (OpenCode
adapter implements it) · SKELETON-OMP (the omp adapter skeleton type-checks against the port and passes an offline
fixture; **not** wired to a running host, not shipped) · LEGACY-GONE (old host-shaped contract deleted).

## Ports

| ID | Port (namespace `Wanxiangshu.Host.Ports`) | File | Owner lane | Replaces (legacy) | Status |
|---|---|---|---|---|---|
| P-01 | `ISessionPort`, `SessionSpec`, `SessionInfo` | `Host/Ports/Sessions.fs(i)` | fork (+ Execution/Session) | `IOpenCodePort` session members, `ISessionHostPort` create/list/parent, `OpenCodeChildOptions`, `OpenCodeChildInfo` (legacy contract and types remain live across Fork, SyncDelegate, Advisor, and OpenCode wiring) | DECLARED, WIRED-OC, SKELETON-OMP — all seven verbs (`Create`, `Parent`, `Children`, `Abort`, `Interrupt`, `Close`, `FindByTitle`) implemented in the OpenCode adapter (`SdkSessionPort` / `HttpSessionPort`, `OpenCode/Host/SessionSnapshotPort.fs`); every verb reports failures as `HostError`, and no verb is left `Unsupported`. Product consumers documented as left on legacy `ISessionHostPort` |
| P-02 | `IPromptPort`, `PromptRequest`, `DeliveryVerdict` | `Host/Ports/Prompts.fs(i)` | fission (+ Interaction/Dispatch) | `OpenCode.IPromptPort`, `OpenCodePromptOptions`, `DetachedSendVerdict` | DECLARED, SKELETON-OMP |
| P-03 | `ITranscriptPort`, `TranscriptMessage`, `TranscriptPart` | `Host/Ports/Transcript.fs(i)` | fork (+ Execution/Session) | `ISessionSnapshotPort`, `SessionMessage` (both still live; cutover in progress) | DECLARED, WIRED-OC, SKELETON-OMP — `SessionRecoveryHost` and the dispatch recovery path (`PromptRecovery` + `RecoverySurface`) now read through `ITranscriptPort`; fork recovery (`ChildRecoveryWorkflow`) deferred due to `ToolRuntimeScope` fanout (24 files) and tool-part dependencies; full 44-file leftover inventory documented in root `保姆级交接与展望.md`; remaining consumers still to be routed (O-1: the address IS the omp persisted entry id; the persisted entry list and the live `AgentSession.messages` are not the same set, and one malformed entry now fails the whole read with `HostError.Transport` — which view the neutral transcript means is an open item for the port owner) |
| P-04 | `IToolRegistrar`, `ToolSpec`, `ToolInvocation`, `ToolOutcome` | `Host/Ports/Tools.fs(i)` | tools | `OpenCode/Tools/ToolContext` `Tool`/`ToolContext`/`ToolOutput` | DECLARED, SKELETON-OMP |
| P-05 | `IToolGate`, `ToolCallVerdict`, `ToolResultEdit`, `ToolDefinitionEdit` | `Host/Ports/ToolGates.fs(i)` | tools | `tool.execute.before/after`, `tool.definition` handling in `OpenCode/Plugin/PluginHooks` | DECLARED, SKELETON-OMP |
| P-06 | `ITurnTransform`, `TurnAdmission`, `ContextEdit`, `ParamsVerdict` | `Host/Ports/TurnTransforms.fs(i)` | plugin-host | `OpenCode/Plugin/PluginTransforms`, `ChatParamsHook`, `ProviderSystemTransform` entry points | DECLARED, SKELETON-OMP |
| P-07 | `ISignalSource`, `HostSignal`, `TurnTerminal` | `Host/Ports/Signals.fs(i)` | plugin-host | `OpenCode/Signals/HostSignal`, `HostEventCodec` terminal types | DECLARED, SKELETON-OMP |
| P-08 | `IModelCatalog`, `ModelRoute`, `SpawnRoute` | `Host/Ports/ModelRouting.fs(i)` | plugin-host | `OpencodeModel` (Foundation), `ModelRoutingSurface` host calls — both **still live**; retiring them is HNI-10, not this slice | DECLARED, WIRED-OC, SKELETON-OMP |
| P-09 | `ICompactionGate`, `CompactionContribution`, `ContinueVerdict` | `Host/Ports/Compaction.fs(i)` | plugin-host | `HostCompactionGate` hook entry | DECLARED, SKELETON-OMP |
| P-10 | `IHostPaths` | `Host/Ports/Storage.fs(i)` | arch | ad-hoc `input.directory`/`project` reads — still live at `OpenCode/Host/PluginHost.fs:41-51`, `OpenCode/Host/OpenCodePort.fs:105-120`, `OpenCode/Host/SessionSnapshotPort.fs:340-346`, `OpenCode/Plugin/PluginBoot.fs:64,107` | DECLARED, WIRED-OC, SKELETON-OMP — `OpenCode/Host/PathsPort.fs(i)` implements `IHostPaths`; `StateRoot` is the product's existing `RuntimePath.stateRoot` (`<primary worktree>/.Herdchestra`, supplied already-resolved because the port is a plain property while the resolution is async), `ProjectRoot` is `input.directory`, `ProjectId` is the host-supplied `input.project.id` (no product code keys on a project id today) with the directory as fallback |
| P-11 | `IHostPlugin`, `HostPorts`, `ProductHooks`, `HostCapabilities`, `IAgentCatalog` | `Host/Ports/Lifecycle.fs(i)` | plugin-host | `OpenCode/Plugin/Plugin`, `PluginBoot`, `ManagedAgentConfig` host side | DECLARED, SKELETON-OMP |
| P-12 | `IDiagnosticSink` | `Host/Ports/Diagnostics.fs(i)` | plugin-host | ad-hoc `Diagnostic.emit` diagnostics — still live, e.g. `OpenCode/Host/PluginHost.fs:54`, `OpenCode/Host/Sessions.fs:392,402,411`, `OpenCode/Host/HostSignalBootstrap.fs:277,371`, `OpenCode/Host/HostCompactionGate.fs:26`, `OpenCode/Host/HookPolicySurface.fs:77`, `OpenCode/Plugin/PluginHooks.fs:494,503,549`; the `client.app.log` call the column previously named does not exist in the product yet | DECLARED, WIRED-OC, SKELETON-OMP — `OpenCode/Host/DiagnosticsPort.fs(i)` implements `IDiagnosticSink` over the host's own log verb `client.app.log` (`POST /log`); `Emit` is best-effort: a failed log write is dropped and never thrown into product code |
| P-19 | `IHostConfigPort`, `RequiredSetting`, `HostSettingValue`, `SettingGap` | `Host/Ports/HostConfig.fs(i)` | plugin-host | `HostCompactionGate.enforceSettings`/`writeSetting` host-side writes, `config`-hook mutation | DECLARED, SKELETON-OMP |
| P-00 | `HostError`, `HostResult`, `HostCapability`, `WorkspacePath`, `AgentRole`, `CanonicalJsonText`, `JsonSchema`, `HostFailure`, `TurnTerminal` (shared vocabulary; ids `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress` come from `Foundation.Identity`) | `Host/Ports/Vocabulary.fs(i)` | arch | `string` error channels on host ports | DECLARED, SKELETON-OMP |

## Lane-declared domain seams (tier B, namespace `Wanxiangshu.Host.Domain`, files `Host/Domain/*.fs(i)`) — ID reservations

Domain lanes declare seams their domain needs from a host in `src/Wanxiangshu/Host/Domain/<Name>.fs(i)`
(DESIGN §1.1, tier B). IDs are reserved here so parallel lanes never collide (HNI-11 found two `P-13`s).
Use the ID below in your row; next free ID: **P-19**. A tier-B file is **not** a neutral port: it is never
mirrored to the TypeScript port surface, and the adapters' references to it are ratcheted (gate ⑬).

**The fork lane's ports (P-13/P-14/P-15) are on `master`.** `Host/Domain/CommandDeclaration.fs(i)`,
`Host/Domain/DelegationCaller.fs(i)` and `Host/Domain/DelegationCommand.fs(i)` landed on `master` through the fork lane's six
commits `58660d4d9`, `2e9d5025d`, `6c6b8cfdf`, `a4222e3fb`, `e16e72c34` (slice A) and `aa43d3455` (slice B,
`Fork/JoinRules`). The fork lane's branches `origin/wip/hni-fork` and `origin/wip/hni-fork-next` are **deleted**
(their content is on `master`).
The other lanes' ports are **not all still off `master`**: P-16 (`Host/Domain/PairGuidance.fs(i)`) is on
`master` (landed by the plugin-host lane through `3e2f12213`, whose subject line says "P-13" — the **reserved
id is P-16**, see drift **D-01**) and P-18 (`Host/Tools.fs(i)`) is still on `origin/wip/hni-tools` — that
branch still exists on the remote (measured 2026-10-21).
**P-17 (`Host/FissionPort.fs(i)`) is now on `master`**: the fission lane landed
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
Namespace note: the lane seams are **tier B** and use `Wanxiangshu.Host.Domain` under `Host/Domain/`, while the
neutral ports are **tier A** and use `Wanxiangshu.Host.Ports` under `Host/Ports/` (see DESIGN §1.1 and drift
**D-05**). Every seam file carries its `HNI P-xx` marker as a **plain `//` comment above the `namespace` line**
(drift **D-06**).

**No compile/test evidence for the fork lane.** The fork lane was pushed under the owner's "no build before push"
rule: only the non-compiling gates were run (`architecture.mjs`, `adapter-thinness.mjs`, `provider-leak-gate.mjs`).
The delivery team must build master at `aa43d3455` or later to get compile/test evidence for P-13/P-14/P-15.

| ID | Port | File | Owner lane | Notes (HNI-11 review) |
|---|---|---|---|---|
| P-13 | `CommandDeclaration`, `CommandArgument`, `ArgumentShape`, `ICommandArguments` | `Host/Domain/CommandDeclaration.fs(i)` | fork | **Tier B** (domain seam, namespace `Wanxiangshu.Host.Domain`). Neutral and product-shaped. Converges with P-04: `ArgumentShape` is what a `ToolSpec.Schema` is generated from. Status: WIRED-OC, **on `master`** (`58660d4d9`).
| P-14 | `IDelegationCallerHost`, `DelegationCaller` | `Host/Domain/DelegationCaller.fs(i)` | fork | **Tier B**. D-03: carries `AgentJournal` (product persistence) and fork runtime — a composition seam, not a host port. Status: WIRED-OC, **on `master`** (`58660d4d9`). Convergence (HNI-14): the interface splits — `DelegationCaller` (the host's contribution: session / provider run / tool-call ids) stays a neutral record in `Host/Domain/`; everything else becomes the product record `DelegationEnvironment` (`Execution/Delegation/Fork/`, not under `Host/`).
| P-15 | `IDelegationCommandHost`, `DelegationReply` | `Host/Domain/DelegationCommand.fs(i)` | fork | **Tier B**. D-03 as P-14. Status: WIRED-OC, **on `master`** (`58660d4d9`). Convergence (HNI-14): **no member of P-15 is host-supplied** — it is dissolved into the product record `DelegationEnvironment`; the two host-typed members (`ManagerCapabilityFactsFor`, `CallerRole`) are product types, and the interface file is deleted rather than kept.
| P-16 | `PairGuidanceTranscript`, `PairGuidanceLedger`, `PairGuidanceOccurrence` | `Host/Domain/PairGuidance.fs(i)` | plugin-host | **Tier B**. Was proposed as "P-13" (collision D-01). Feeds P-06 `ContextMessages`. Status: **on `master`** (`3e2f12213` — the commit subject says "P-13", the reserved id is **P-16**; the branch `origin/wip/hni-plugin` still exists but the file has landed). Status **WIRED-OC**: the OpenCode adapter (`OpenCode/Host/PairProgrammingThoughtTransform`) decodes the transcript and renders only; the product owner is `Interaction/PairGuidance/Occurrences` (ledger, ordinal, placement, commit order) Convergence (HNI-14): split by ownership — `PairGuidanceTranscript` stays a decoded host fact in `Host/Domain/`; `PairGuidanceOccurrence` + `PairGuidanceLedger` move to the product (`Interaction/PairGuidance/`), and `Append`'s error becomes a **product** error DU, not `HostError` (it writes the product's own journal).
| P-17 | `FissionHostPort`, `FissionOwnerRuns`, `FissionPtyCompletion`, `FissionPtyStatus`, `FissionSessionPort`, `FissionEventPort` | `Host/FissionPort.fs(i)` | fission | WIRED-OC. Declared in namespace `Wanxiangshu.Host.Ports` (drift **D-05**) but the file stays outside `Host/Ports/`, so gate ⑫ does not treat it as a neutral family. D-03 partly resolved: `AgentJournal` left the port (it is threaded into the launch call instead); the legacy `ISessionHostPort` / `IEventObservationPort` remain until P-01 / P-07 land. S3 (2026-10-22): the Fission lane now speaks its **own** names — `FissionSessionPort` and `FissionEventPort` — and the legacy Foundation types are reachable only through those two **transitional** aliases, which are the migration seam P-01 / P-07 will re-point. `FissionHostPort.Sessions` / `.Events` and the whole `Execution/Fission/Convergence` seam use the Fission-owned names; the aliases are transparent, so behaviour is unchanged. S4 (2026-10-22): the `fission` tool's **product policy** left the adapter — the prose vocabulary, the prose rendering and the office-entitlement predicate now live in the product lane (`Execution/Fission/ToolPolicy.fs(i)`, `FissionToolPolicy`); the adapter keeps only the wire mapping (rejection → semantic path) and the wire encoding. No port shape changed. Status: **on `master`** (`2680bc07d`, `0287a2349` — the port moved into the `Wanxiangshu.Host.Ports` namespace — `9894b1e0c`, and the S3 / S4 slices); `origin/wip/hni-fission` is **deleted**. **No typed `PtyId` is introduced** — a PTY id is minted by the product's run registry, not the host.
| P-18 | `HostToolContext`, `ToolAdmission` (declared authority: `OfficeRole` \| `PrivateAttachment`); `ToolResultText` is a **module** (product prose), not a type | `Host/Tools.fs(i)` | tools | Was proposed as a redefinition of "P-04" (D-02). P-04 stays the registration port in `Host/Ports/Tools.fs`; P-05's call verdict is `ToolCallVerdict` (renamed to match the tools lane). **D-07: this file name collides with P-04's `Host/Ports/Tools.fs(i)` — rename before moving it under `Host/Ports/`.** Status: **not on `master`** — the file lives on `origin/wip/hni-tools` (branch still exists, measured 2026-10-21). Convergence (HNI-14): **P-18 is voided and its id is retired (not reused)** — `HostToolContext` folds into P-04 `ToolInvocation`, `ToolAdmission` into `ToolSpec.Admission`, `ToolResultText` into a product module; the branch file is deleted, so D-07 disappears with it.

### Drift findings (HNI-11, 2026-10-21) and the convergence target
- **D-01** ID collision (`P-13` used by fork and plugin-host) → plugin-host pair guidance is **P-16**.
- **D-02** tools lane redefined P-04 → recorded as **P-18**. Convergence: `HostToolContext` and `Ports.ToolInvocation` describe the
  same call; target one type with typed `SessionId` (not `string`), `AgentRole` (not `string`), cancellation as a token.
  `ToolResultText` (TOML rendering of product results) is product prose, not a host seam → move to a product module.
  **Resolved (HNI-14, 2026-10-22)**: `HostToolContext` folds into P-04 `ToolInvocation`, `ToolAdmission` into
  `ToolSpec.Admission`, `ToolResultText` into a product module; **P-18 is voided and its id retired (not reused)**.
  Decision record: `evidence/hni14-convergence.md` §3.5.
- **D-03** P-14/P-15/P-17 hand the domain an `AgentJournal` and other product runtime objects "from the host". A host never owns
  product state (DESIGN §2 P-10). Convergence: split each into (a) neutral host ports from `HostPorts` (P-01 sessions, P-02 prompts,
  P-07 signals) and (b) product-composition inputs supplied by `Composition/`. Card HNI-14.
  **Resolved to a contract (HNI-14, 2026-10-22)**: P-14/P-15 become one product record (`DelegationEnvironment`) plus one
  neutral record (`DelegationCaller`); P-16 splits transcript (host fact) from ledger (product); P-17 shrinks to the two
  neutral ports P-01/P-07. Each owning lane owns a numbered step. Decision record: `evidence/hni14-convergence.md` §3.2–§3.4.
- **D-04** lane ports use `Result<_, string>` for host round trips; neutral ports use `HostResult<_>` (`HostError`). Convert when the
  lane switches from legacy `ISessionHostPort` to P-01.
  **Resolved to a rule (HNI-14, 2026-10-22)**: inside `src/Wanxiangshu/Host/**` a host round trip uses
  `HostResult<_>` / `HostError`; a parse verdict may keep `Result<_, unit>`; `Result<_, string>` in product code is out
  of scope. Measured population: 7 declarations (`evidence/hni14-convergence.md` §2.5), converted by the §3.2–§3.4 steps.
- **D-05** lane ports declare `namespace Wanxiangshu.Host`, the same namespace many adapter files `open` for `CanonicalJson`/`HostDigest`.
  The neutral ports were moved to `Wanxiangshu.Host.Ports` for exactly that reason (DESIGN §1.1); the lane ports should move to a
  namespace that is not `open`ed by adapters (e.g. `Wanxiangshu.Host.Domain`) or the same shadowing risk applies to them.
  **Status: RESOLVED (2026-10-22).** P-13/P-14/P-15/P-16 moved to `Host/Domain/` with namespace `Wanxiangshu.Host.Domain`;
  gate ⑬ keeps the two tiers apart. P-17 already used the safe `Wanxiangshu.Host.Ports` namespace — only its **location**
  remains open (HNI-14).
- **D-06** the lane port files (`Host/*.fs(i)`) carry no `HNI P-xx` header marker. The TS mirror generator
  discovers the port families from `src/Wanxiangshu/Host/Ports/*.fsi` and keys on that marker; a family without one
  is a **hard error**, not a silent skip. The lane files sit directly under `Host/` today, so they are not
  discovered — but the moment a lane moves one under `Host/Ports/` (which HNI-14 intends) the mirror generation
  fails until the file declares its reserved id. A lane that lands a new family must add the `HNI P-xx` marker to
  the file header.
  **Status: PARTLY RESOLVED (2026-10-22).** Every tier-B file now carries its `HNI P-xx` marker as a **plain `//`
  comment above the `namespace` line** (a `///` doc comment between `namespace` and `open` would be FS3390 and would
  be dropped). A tier-B file is *not* discovered by the mirror generator (it reads `Host/Ports/*.fsi` only), so the
  marker is documentation plus the seam's identity, not a mirror requirement — but a seam that later moves into
  tier A must keep the marker and satisfy the mirror generator.
- **D-07** the tools lane's `Host/Tools.fs(i)` has the **same file name** as P-04's `Host/Ports/Tools.fs(i)`.
  Today the two live in different directories, so nothing breaks; but the moment the tools lane moves its file
  under `Host/Ports/` (which HNI-14 intends) it **overwrites P-04** — the registration port — and the
  `Tools.fs`/`Tools.fsi` pair the mirror generator reads would be the wrong one. The tools lane must rename its
  file (e.g. `Host/Ports/ToolContext.fs(i)`) before it moves it. No type name collides today: the tools lane
  declares `HostToolContext`/`ToolAdmission`, P-04 declares `ToolInvocation`/`ToolOutcome`; the **file name**
  is the collision, not the type name.
- Lane domain seams are not under gate ⑪ (port-purity covers `Host/Ports/` only) and are not under the TS
  mirror generator (it reads `Host/Ports/*.fsi` only). Gate ⑬ covers them instead: it checks that a tier-B
  file declares `Wanxiangshu.Host.Domain` and lives under `Host/Domain/`, and it ratchets the adapter files
  that reference the seam.

**P-19 (2026-10-22, plugin-host lane).** Declared: the host's activation config as product-shaped
data. Status is DECLARED only — no adapter implements it yet. Notably absent by design:
`ConfigureAgents` (projecting the P-11 role catalog onto host agent entries, `ManagedAgentConfig`)
is **not** part of P-19, because it would make `HostConfig` depend on `AgentRoleSpec` from
`Lifecycle` while `HostPorts` depends on `HostConfig`; it needs its own reserved id when a lane
takes it. P-11's `Lifecycle` row gains `HostPorts.Config`, and `ProductHooks.ToolGate`, `.Turn` and
`.OnSignal` become `option`: a member the product does not decide yet is absent, not a pass-through
shim (DESIGN rule 4 + rule 8). No consumer exists at this commit, so the shape change is inert.

### What a lane must do when it lands a domain seam (the D-05/D-06/D-07 checklist)

These are the concrete steps, so a lane does not have to re-derive them. **Step 0 decides the tier**:

0. **Choose the tier first (DESIGN §1.1).** A type belongs in **tier A** (`Host/Ports/`,
   `Wanxiangshu.Host.Ports`) only if the product core needs it *from* a host in neutral vocabulary — that is
   what the TS mirror and the omp adapter consume. A type that is **the lane's own seam** — the domain speaks
   it and a host must supply it, but it is not a neutral vocabulary — belongs in **tier B** (`Host/Domain/`,
   `Wanxiangshu.Host.Domain`). A type that no host supplies is product code and does not belong under `Host/`
   at all.
1. **Tier A only: move the file under `src/Wanxiangshu/Host/Ports/`** — that directory is what the mirror
   generator and gate ⑪ (port-purity) both look at. A tier-A file left directly under `Host/` is invisible to
   both, and gate ⑬ reports it. **Tier B: put the file under `src/Wanxiangshu/Host/Domain/`.**
2. **Tier A only: rename the file if its base name is taken** by a neutral family. The names already taken are
   `Vocabulary`, `Sessions`, `Prompts`, `Transcript`, `Tools`, `ToolGates`, `TurnTransforms`, `Signals`,
   `ModelRouting`, `Compaction`, `Storage`, `Lifecycle`, `Diagnostics` (**D-07**). The family name is the
   `.fsi` base name, so it is also the TS mirror section name.
3. **Declare the reserved id in the header**: a line containing `HNI P-xx` where `xx` is your reserved id from
   the table above (P-13 … P-18). The generator matches `/\bHNI (P-\d{2})\b/` anywhere in the header; a family
   without one is a **hard error**, not a silent skip. Put it in a **plain `//` comment above the `namespace`
   line** — a `///` doc comment between `namespace` and the first `open` is FS3390 and is dropped, e.g.
   `// HNI P-13: the command vocabulary a host registers (tier B; never mirrored).`
4. **Use the namespace of the tier you chose**: `Wanxiangshu.Host.Ports` for tier A, `Wanxiangshu.Host.Domain`
   for tier B. `Wanxiangshu.Host` is `open`ed by **75 files** under `src/Wanxiangshu/` (measured 2026-10-22;
   81 `open` lines, mostly for `CanonicalJson`/`HostDigest`), so a type named e.g. `Digest` in that namespace
   shadows the real one at every one of those call sites. Gate ⑬ rejects a tier namespace declared outside its
   tier folder (**D-05**). `Wanxiangshu.Host.Ports` had **zero** openers when it was created and the openers it
   has today (**14 files**, measured on `origin/master` 2026-10-22) were each added deliberately;
   `Wanxiangshu.Host.Domain` has **11 files** (the seam's own consumers, measured on `origin/master`
   2026-10-22), and the tier-B ratchet stops that set from growing. The risk **D-05** guards against is
   therefore not "some file `open`s the namespace" but "a **pre-existing** namespace with 70+ openers": port
   names placed in `Wanxiangshu.Host` would shadow real types across every one of those call sites, whereas a
   namespace nothing opened before is safe to add to. **An `open` is not an implementation** — see the status
   column for whether a port is actually wired.
5. **Reuse the neutral vocabulary** (DESIGN §2): `HostResult<_>`/`HostError` instead of `Result<_, string>`
   (**D-04**), and `SessionId`/`ToolCallId`/`PromptKey`/`TranscriptMessageAddress`/`ModelRoute`/`AgentRole`/
   `CanonicalJsonText` from `Host/Ports/Vocabulary.fs(i)` instead of `string`.
6. **Do not hand-edit** `adapters/omp/ports.ts`; it is generated. Run `adapters/omp/check.sh` — it regenerates
   the mirror and fails on any byte difference. A tier-B file is **not** mirrored, so it never changes the
   generated file.
7. **Update your row** in the table above (Status, Replaces) in the same commit.

## Adapter folders (implementations, not ports)

| Host | Folder | Owner lane |
|---|---|---|
| OpenCode | `src/Wanxiangshu/OpenCode/**`, `src/Wanxiangshu/**/OpenCode/**` | per domain lane (fission, fork, tools, plugin-host) |
| omp | `proposals/host-neutral-interface/adapters/omp/` (TS skeleton, offline fixtures) + `src/Wanxiangshu/Omp/Adapter.fs(i)` (F# adapter, in shard `Wanxiangshu.Owner.host-boundary.omp-adapter.fsproj`) + `src/Wanxiangshu/Host/SecondHost/HostPlugin.fs(i)` (P-11 product composition root) + `src/Wanxiangshu/Composition/Omp/OmpExtension.fs(i)` (extension entry) | arch |

### SKELETON-OMP (what it means now)

`proposals/host-neutral-interface/adapters/omp/` is a **type-checked skeleton with an offline fixture**: the
generated TS mirror is byte-identical to the F# signatures, the adapter type-checks against omp 18.12.4's own
types, and the fixture drives it offline. It is **not** wired to a running host and **not** shipped.

The five decided rules in `evidence/omp-feasibility.md` ("Decided rules (was: open items)", O-1..O-5) are
**binding on `cut36-omp-2`**, the lane that builds the real omp adapter:

- **O-1** — the transcript message address is the omp `SessionEntry.id`; never a positional index.
- **O-2** — provider-param encode is adapter-only, per provider family, and fails closed.
- **O-3** — the omp binding contract: hand-written, product-free entry module; the Fable-built product is
  passed in as a plain `IHostPlugin` value.
- **O-4** — a path-shaped `parentSession` is resolved through the host's lookup, or the call fails with
  `HostError.NotFound` carrying the raw value.
- **O-5** — the neutral `SessionId` is always the persisted id, never omp's provider id.

`cut36-omp-2` implements these rules; it does not re-decide them. The same rules are summarised in
`DESIGN.md` §3.1 (binding contract) and §3.2 (provider-params encode rule).

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
- 2026-10-22 eng-hni-host (P-01 corrective): the OpenCode adapter's P-01 session port now reports **every** failure
  as `HostError` — 404 → `HostError.NotFound`, other 4xx → `HostError.Refused`, 5xx → `HostError.Transport`, a thrown
  exception → `HostError.Transport`. The mapping is applied twice, once on the SDK envelope path (a non-2xx answer
  arrives as an error envelope, not as a throw) and once on the raw-HTTP path; the host payload itself never leaves
  the adapter. Two verbs were wrong before this change and are corrected: the HTTP `Close` called
  `POST /session/{id}/abort` (a copy of `Abort`) and now calls the host's removal verb `DELETE /session/{id}`, and the
  SDK `Close` now sends the route payload the SDK expects (`path.id` plus the session id). `Interrupt` is **not** a
  capability downgrade: the OpenCode host API generation this adapter speaks (v1) exposes exactly one stop action per
  session, `POST /session/{id}/abort`, and no attempt-scoped interrupt route — the host's `session.interrupt` belongs to
  the v2 API generation (`POST /api/session/{sessionID}/interrupt`), which this adapter does not speak. The
  product's own attempt-interrupt path already calls `client.session.abort` for the session that owns the attempt
  (`OpenCode/Host/Sessions.fs` → `OpenCode/Host/OpenCodePort.fs`), so `Interrupt` maps onto that same wire verb and is
  stated as such in the code. **No verb is left `Unsupported`.** No new port: P-01 (`Host/Ports/Sessions.fs(i)`) is
  unchanged, so the next free port id stays **P-19** and the generated TS mirror is unaffected (`omp: n/a`).
  **No compile/test evidence** (owner's no-build-before-push rule) — the delivery team must build master and run the
  session requirement suites.


- 2026-10-22 eng-host-arch: **the two Host/ namespace tiers are now separate directories.** P-13
  (`CommandDeclaration`), P-14 (`DelegationCaller`), P-15 (`DelegationCommand`) and P-16 (`PairGuidance`) moved
  from `src/Wanxiangshu/Host/` to `src/Wanxiangshu/Host/Domain/` and now declare
  `namespace Wanxiangshu.Host.Domain` (tier B); the twelve consumers that used them now `open
  Wanxiangshu.Host.Domain` instead of `Wanxiangshu.Host`. **Drift D-05 is resolved, D-06 partly resolved**
  (each seam file carries its `HNI P-xx` marker as a plain `//` comment above the `namespace` line). P-17
  (`Host/FissionPort.fs(i)`) already declared the safe `Wanxiangshu.Host.Ports` namespace and is **not**
  moved — only its location is open, tracked on HNI-14. New **gate ⑬** (namespace-tier integrity) in
  `scripts/checks/adapter-thinness.mjs`: a tier-A file must declare `Wanxiangshu.Host.Ports`, a tier-B file
  `Wanxiangshu.Host.Domain`, no file directly under `Host/` may claim either namespace, and the adapter files
  that reference the tier-B seam are ratcheted by `scripts/checks/domain-seam-baseline.json` (4 files). The
  tier-B ratchet records existing debt; it does not forbid the seam from being used. Also corrected here:
  the P-16 row said "not on `master`" (it **is** on `master`, via `3e2f12213`), the paragraph above the table
  said P-16 lives on `origin/wip/hni-plugin` (the file has landed), the "95 files `open Wanxiangshu.Host`"
  figure is now **75 files / 81 `open` lines** (measured 2026-10-22), and the checklist grew a tier choice as
  step 0. **No compile/test evidence** (owner's no-build-before-push rule) — the delivery team must build
  master and run the requirement suites. Gates: architecture OK, adapter-thinness 139 files / 33133 lines,
  product-reference 595 (baseline 689), without-port 137 (baseline 150); `node --test
  requirements/host-boundary/tests/034.test.mjs` 9/9.

- 2026-10-22 eng-integ-027-2: **delegation-027 EMR-010 model-routing lease resolution**. Fixed by `a38ef7770`
  in `HostSignalBootstrap.fs` by keying `admissionInFlight` with `(ChatExecutionKey * ChatAdmissionPurpose)`.
  Separates `PublishInput` (purpose 0, no model-routing lease, generates `DeferredInput`) and `EnterProviderStep`
  (purpose 1, requires active model lease continuance) for the same physical user message ID, preventing the
  latter from prematurely joining the former's promise and raising EMR-010 at `SessionExecutionBinding.fs:116`.
  Static history analysis traces the origin to `48a0bf426` (2026-10-07) and manifestation to `c0246c36d` (2026-10-21);
  suspects `6b7ae3a46`/`cf1f0d2a9`/`e058233a3` are not the structural source cause (timing untested); no commit-level
  binary search was run. Installed-Host canary gap is diagnosed as a lifecycle fence/abortion settlement mismatch
  (unimplemented proposed bridge, canary remains unresolved). **No new port** — the HNI port surface is untouched;
  next free port id remains **P-19**; `omp: n/a`.

- 2026-10-21 eng-hni-plugin-goose (HNI-08): P-16 pair guidance is WIRED-OC — the guideline fold moved out of
  `OpenCode/Host/PairProgramming` into the domain owner `Interaction/PairGuidance/Occurrences`; the OpenCode transform
  decodes the transcript and renders only. The ID is P-16 (D-01: P-13 is the fork lane's `CommandDeclaration`).

- 2026-10-22 eng-cut36-omp: the omp adapter is now **real F# source in three compile shards** — `Omp/Adapter.fs(i)`,
  `Host/SecondHost/HostPlugin.fs(i)` and `Composition/Omp/OmpExtension.fs(i)`, listed in `src/Wanxiangshu/compile-order.txt`
  (lines 1360-1365) and owned by `Wanxiangshu.Owner.host-boundary.omp-adapter.fsproj` /
  `.omp-extension.fsproj` / `.second-host-plugin.fsproj`. Two no-semantic-change flattening commits
  (`caaacdcf5` for `HostPlugin.fs`, `5494db8f5` for `Omp/Adapter.fs`) bring that three-file surface to **0**
  `fsharp-control-pyramid` findings; the repo-wide gate still reports **5** findings, all in
  `OpenCode/Host/SessionSnapshotPort.fs` (444, 473, 629, 648, 715). Gates on this tree: `architecture.mjs` OK,
  `aggregate-retired.mjs` OK, `adapter-thinness.mjs --report` = 141 adapter files / 34547 lines, product-reference
  **595** (baseline 689), without-port **137** (baseline 150) with **no omp file in either map**;
  `shard-edge-audit.mjs` = 245 shards / 268 findings, **high 0 / medium 0** (all 268 are `[low]` type-name edges,
  8 of them touching the three omp shards). **No port status changed** (all remain `DECLARED` / `SKELETON-OMP`):
  the shards exist but have **never been compiled on this branch** — `dist/Composition/Omp/` does not exist, the
  build-slot cap is 180 s (`scripts/build.mjs:78`), while the full-impact rebuild of this tree measured **3059 s**
  (from the build log kept outside the repo, `/home/hermes/work/cut36-omp/build-baseline.log`; the figure is **not**
  reproducible from repo contents), and the real smoke is still FAIL / not passed. **No port is WIRED-OMP.**
- 2026-10-22 eng-hni-host (P-03 consumer cutover 1): the session recovery host now reads the transcript through
  the neutral port. `OpenCode/Host/SessionRecoveryHost.fs(i)`: the constructor argument `snapshot: ISessionSnapshotPort`
  became `transcript: ITranscriptPort`, and the single host read (the provider classification read) moved from
  `snapshot.GetMessages` to `transcript.Messages`. The classifier keeps its semantics: the role compare is
  `TranscriptRole.Assistant`, the parent compare is `TranscriptMessageAddress.create/.value`, the provider-run
  compare is `TranscriptMessageAddress.value message.Id`, and a transcript failure still maps to
  `ProviderPhysicalObservation.ReceiptMissing`. `OpenCode/Host/SessionRecoveryHostSurface.fs` builds an
  `ITranscriptPort` stub at both construction sites; `OpenCode/Host/HostSignalBootstrap.fs` adapts its existing
  snapshot port through `TranscriptPort.HostTranscriptPort` (that file is shared with the signal lane; only the
  constructor argument changed there). **The legacy `ISessionSnapshotPort` / `SessionMessage` contract stays live**
  — the adapter wraps it and the other consumers (turn reconcile, prefix wire, delegation advisor, tools,
  dispatch recovery) are still to be routed. **No new port** — P-03 (`Host/Ports/Transcript.fs(i)`) is unchanged, so
  the next free id stays **P-19**; `omp: n/a` (the family `.fsi` did not change, so the generated TS mirror is
  unaffected). Gates: architecture OK, adapter-thinness 139 files / 33000 lines, product-reference 595
  (baseline 689), without-port 134 (baseline 149), fsharp-control-pyramid 2 findings — both pre-existing in
  `OpenCode/Host/SessionSnapshotPort.fs`. **NOT BUILD-VERIFIED, NOT TEST-RUN** (owner's no-build-before-push rule) —
  the delivery team must build master and run the recovery requirement suites.

- 2026-10-22 eng-hni-host (P-03 consumer cutover 2 — dispatch recovery): the dispatch recovery path now reads the
  transcript through the neutral port. `Interaction/Dispatch/Recovery.fs(i)`: `PromptRecovery.reconcile` takes
  `transcriptOpt: ITranscriptPort option` (was `snapshotOpt: ISessionSnapshotPort option`); the single host read moved
  from `snapshot.GetMessages` to `transcript.Messages`, and a transcript failure is rendered to the recovery reason by a
  private `HostError -> string` helper (transport/refusal reasons passed through verbatim; `Unsupported` spells
  `Host capability <cap> is unsupported`). `Interaction/Dispatch/RecoverySurface.fs`: the private `Snapshot`
  (`ISessionSnapshotPort`) became `Transcript` (`ITranscriptPort`) built over the same `SessionSnapshotPort.projectMessages`
  projection, and the unreadable fixture now fails with `HostError.Transport reason` so the reason text stays verbatim.
  The claim law (`DispatchLaw.recover`, role=`user` + `PromptKey` among the newest `RecoveryTailWindow` messages) is
  unchanged; a host role outside the neutral vocabulary is carried as `system` (it could never prove a landing) so the
  tail window width is preserved exactly. **The legacy `ISessionSnapshotPort` / `SessionMessage` contract stays live** —
  fork recovery (`ChildRecoveryWorkflow.Ports` / `Fork/Host/Restart.fs`) was evaluated for cutover but deferred: `HostForkRuntime`
  and `ToolRuntimeScope` fan out into 24 tool files (126 callsites), and upstream tools require snapshot-specific `SessionToolPart` /
  `ToolParts` / `locateToolCall` absent from neutral `TranscriptPart.ToolUse`; per HNI policy against broad cross-lane fanout and lossy
  reverse projection, fork recovery remains on `ISessionSnapshotPort` until downstream tools complete domain extraction. The full
  44-file leftover inventory is documented in root `保姆级交接与展望.md`. Remaining consumers (turn reconcile, prefix wire, delegation
  advisor/seat, tools, fork host restart) are still to be routed. **No new port** — P-03 (`Host/Ports/Transcript.fs(i)`) is unchanged;
  the next free id stays **P-19**. Gates:
  architecture OK, adapter-thinness 139 files / 33004 lines, product-reference 589 (baseline 689), without-port 134
  (baseline 149), fsharp-control-pyramid 2 findings — both pre-existing in `OpenCode/Host/SessionSnapshotPort.fs`.
  **NOT BUILD-VERIFIED, NOT TEST-RUN** (owner's no-build-before-push rule) — the delivery team must build master and run
  the dispatch-protocol / effect-accounting recovery requirement suites.
- 2026-10-22 eng-hni-host (host-lifecycle leftover audit): documented current lifecycle status across `保姆级交接与展望.md`,
  `BOARD.md`, and this registry without starting an unverified broad cross-lane migration. The OpenCode adapter
  implementation of `ISessionPort` (`SessionSnapshotPort.fs`, `00ae8c156`) is complete for all 7 verbs.
  The legacy `ISessionHostPort` (`Foundation/HostSessionPort.fs(i)`), `OpenCodeChildOptions`, and `OpenCodeChildInfo`
  (`Foundation/HostSendContract.fs:37-46`) remain actively used and un-retired across four product consumer clusters:
  (1) Fork (`Execution/Delegation/Fork/Host/Agent.fs`, `HostForkRuntime`), (2) SyncDelegate (`Execution/Delegation/SyncDelegate/Runtime.fs`),
  (3) Advisor (`Execution/Delegation/Advisor/Seat.fs`), (4) OpenCode host wiring (`OpenCode/Host/Sessions.fs`, `PluginHost.fs`).
  Documented key semantic mismatches (e.g. `AdvisorSeat`'s need for `SubscribeTerminal` not provided by `ISessionPort`,
  and `InjectedSessionPort`'s family root resolution). Documentation only: no port family file changed, no id or port shape
  touched, so the next free id is unaffected (**P-20** — P-19 was declared as `HostConfig` by `93a51ca15`).
- 2026-10-22 eng-host-arch (HNI-13): the five omp open items O-1..O-5 are **decided** — they now live in
  `evidence/omp-feasibility.md` ("Decided rules (was: open items)") and are **binding on `cut36-omp-2`**, the lane
  that builds the real omp adapter. `DESIGN.md` gains §3.1 (omp binding contract) and §3.2 (provider-params encode
  rule). No port id was added, renamed or renumbered; no F# source was touched; nothing was compiled.

- 2026-10-22 eng-host-arch (slice S3): P-03 is WIRED-OC — the OpenCode adapter now implements
  `ITranscriptPort`. The adapter (`OpenCode/Host/TranscriptPort.fs(i)`) is a *projection* over the existing
  `ISessionSnapshotPort` plumbing: it reuses the snapshot port untouched and translates the host-shaped
  `SessionMessage` into the neutral `TranscriptMessage`. Host role strings map strictly
  (`user`/`assistant`/`system`); an unrecognised role refuses the whole transcript with
  `HostError.Unknown` instead of coercing, and a snapshot failure becomes `HostError.Transport`. Tool parts are
  joined to the snapshot tool parts by call id — a tool part with no matching snapshot part stays an opaque
  `Other "tool"` part, and a snapshot tool part no message part mentions is still projected, so no call is lost.
  The raw host error name is carried as `TurnTerminal.Failed` with `HostFailureKind.Provider`; classifying it into
  an execution failure or a retry decision stays product logic. **The legacy `ISessionSnapshotPort` /
  `SessionMessage` contract is deliberately still live** — many consumers remain, and routing them onto P-03 is
  the next slice; no consumer was cut over in this commit. **No new port** — P-03 (`Host/Ports/Transcript.fs(i)`)
  is unchanged, so the next free id stays **P-19**; `omp: n/a` (the family `.fsi` did not change, so the
  generated TS mirror is unaffected). Gates: architecture OK, adapter-thinness 139 files / 32831 lines,
  product-reference 595 (baseline 689), without-port 136 (baseline 149), fsharp-control-pyramid 5 findings — all
  pre-existing in `OpenCode/Host/SessionSnapshotPort.fs`, 0 in the new file. **No compile/test evidence** (owner's
  no-build-before-push rule) — the delivery team must build master and run the transcript requirement suites.

- 2026-10-22 eng-host-arch (HNI-14): **decision record, design only** — `evidence/hni14-convergence.md` fixes the
  tier rule (a seam belongs under `Host/` only if a host supplies it: tier A `Host/Ports/`, tier B `Host/Domain/`,
  product elsewhere) and the per-port target. **D-02 resolved**: `HostToolContext` → P-04 `ToolInvocation`,
  `ToolAdmission` → `ToolSpec.Admission`, `ToolResultText` → a product module, **P-18 voided and its id retired**.
  **D-03 resolved to a contract**: P-14/P-15 → one product record `DelegationEnvironment` + one neutral record
  `DelegationCaller`; P-16 → transcript stays a host fact, ledger becomes product (its `Append` error is a product
  DU, not `HostError`); P-17 → shrinks to P-01/P-07, `DirectoryFor` absorbed by `SessionInfo.Workspace`,
  `WorkspaceDirectory` by P-10. **D-04 resolved to a rule**: inside `Host/**` a host round trip uses
  `HostResult<_>`/`HostError`; measured population 7 declarations, 13 grep lines. P-13 stays tier B.
  Rows P-14..P-18 carry the target; the owning lanes (fork/session, fission, tools, arch) land the numbered steps
  in §3.2–§3.4. **Nothing was compiled, built or tested on this machine** — the evidence is static (`git grep` /
  `git ls-tree` on `origin/master` `94218a2be`, plus `scripts/checks/architecture.mjs` printing `architecture: OK`).
- 2026-10-22 eng-host-arch (slice P-08): P-08 is WIRED-OC — the OpenCode adapter now implements `IModelCatalog`
  (`OpenCode/Host/ModelCatalogPort.fs(i)`, namespace `Wanxiangshu.OpenCode`). `Available` reads the host catalog
  over the SDK verb `client.config.providers` (`GET /config/providers`, the only host verb that lists every
  provider together with its models) or over `GET /config/providers` on the HTTP server, and maps the host
  payload onto neutral `ModelRoute` values: the provider id becomes `Provider`, the model key becomes `Model`.
  The host catalog carries **no variant field**, so every enumerated route has `Variant = None` (the product's
  `OpencodeModel.variant` is a *reasoning* label set by `ModelRouting.toOpenCodeModel`, not a host-advertised
  variant). An absent provider list is an **empty catalog**, never a failure; a host error becomes
  `HostError.Refused`, a transport failure `HostError.Transport`. `Apply` is honestly
  `HostError.Unsupported HostCapability.ProviderParams`: the host binds a model **per prompt**
  (`OpenCode/Host/OpenCodePort.fs` `modelRequestFields`; per-message `ModelRouting.projectHostModel`), never to a
  session — the session update verb carries only a title — so routing happens at prompt time (P-02
  `PromptRequest.Route`). The adapter chooses no route; choosing is product logic. **The legacy `OpencodeModel`
  (Foundation) and the `ModelRoutingSurface` host calls are deliberately still live** — retiring them is HNI-10;
  no consumer was cut over in this commit. **No new port** — P-08 (`Host/Ports/ModelRouting.fs(i)`) is unchanged,
  so the next free id stays **P-19**; `omp: n/a` (the family `.fsi` did not change, so the generated TS mirror is
  unaffected). Gates: architecture OK, adapter-thinness 140 files / 33248 lines, product-reference 588
  (baseline 689), without-port 134 (baseline 149), fsharp-control-pyramid 2 findings — both pre-existing in
  `OpenCode/Host/SessionSnapshotPort.fs`, 0 in the new file, aggregate-retired OK, subsystems OK.
  **No compile/test evidence** (owner's no-build-before-push rule; the compile slot gave up with exit 75 because the
  machine was heavily loaded) — the delivery team must build master and run the model-routing requirement suites.
- 2026-10-22 eng-master-red (compile repair): restored the `origin/master` compile surface — five F# compile-error fixes
  (`4c84009c2` JoinRun `.fsi` literal + `SessionSnapshotPort.fs` list-append, `94218a2be` `Command.fs` typed `Ok handleId` +
  `SessionsSurface.fs` `TerminalOutcome.*` qualification + `PluginHostWiring.fs` `open`), two malformed-fsproj container fixes
  (`46cf4cab9`, `d0959adb2`), and one `scripts/suites-manifest.sh` bash-fix (`d55fa1eff`). **No port touched** — no id, name or
  port shape changed; next free id stays **P-19**; `omp: n/a`. **No compile/test evidence** (owner's no-build-before-push rule;
  the compile slot gave up at the ~3 min machine cap, exit 124/143) — the delivery team must build master and run the
  requirement suites; per-fix shard ownership is recorded in CHANGELOG.md.



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

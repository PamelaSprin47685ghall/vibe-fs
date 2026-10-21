# HNI-14 — convergence decision for the lane domain ports (D-02, D-03, D-04)

Owner lane: eng-host-arch (arch). Date: 2026-10-22. Baseline: `origin/master` `94218a2be` (the tree containing
`d55fa1eff`). Card: HNI-14. Companions: [DESIGN.md](../DESIGN.md), [REGISTRY.md](../REGISTRY.md),
[BOARD.md](../BOARD.md). This record is **design only**: nothing implemented, compiled or pushed (§6).

## 1. The governing principle

**A seam belongs under `src/Wanxiangshu/Host/` only if a host supplies it.** Tier A — a port:
`Host/Ports/<Family>.fs(i)`, namespace `Wanxiangshu.Host.Ports`, neutral vocabulary only (`HostResult<_>`,
`HostError`, typed ids), under gate ⑪; a host implements it. Tier B — a decoded host fact:
`Host/Domain/<Family>.fs(i)`, namespace `Wanxiangshu.Host.Domain`: what the host *said*, not a verb the product can
call. (The arch lane is landing the `Host/Domain/` move in a parallel slice; this record moves no files.) Product —
anything no host supplies; it does not live under `Host/` at all. **Corollary: a "port" that only the product
implements, and whose members are product decisions, is not a port** — DESIGN §1 rule 3 applies to it *by content,
not by location*. Two consequences decide most of this card: a *composition seam* (a plain record of callbacks) is
**product**, with the repo precedent `Execution/Delegation/Fork/JoinRun.fsi:37-42` (`AgentIdentity`, a product-domain
record of four callbacks); and the tier-B namespace is deliberately **not** mirrored into `adapters/omp/ports.ts`
(the TS mirror is generated from `Host/Ports/*.fsi` only).

## 2. Evidence

All numbers are `git grep` / `git ls-tree` measurements on `origin/master` `94218a2be`.

### 2.1 P-15 adds no host-supplied member

`Host/DelegationCommand.fsi` declares `IDelegationCommandHost` (line 18):

| member | line | what supplies it |
|---|---|---|
| `WorkspaceDirectory: string option` | `:20` | the host (see §3.4) |
| `ViewForSession: string -> Task<string option>` | `:22` | the product (rendering) |
| `ManagerCapabilityFactsFor: string -> Task<ManagerCapabilityFacts>` | `:23` | the product (Foundation type) |
| `CallerRole: unit -> Task<Role option>` | `:25` | the product (Foundation type) |
| `NewHandleId: unit -> string` | `:27` | the product (id minting) |
| `OpenManagerJob` / `ContinueManagerJob` | `:30-41` | the product (orchestrator) |

Correction to the card's draft wording: P-15 **does** carry two product-typed members of its own. What decides the
card is that **not one member of P-15 is host-supplied**; the inherited product-typed members are `Journal:
AgentJournal option` (`Host/DelegationCaller.fsi:20`) and `ForkRuntime: unit -> Result<HostForkRuntime, string>`
(`Host/DelegationCaller.fsi:25`), and everything else the two interfaces add is `string`/`Task`/`Result<_, string>`.

### 2.2 The only implementation delegates to the product's own runtime scope

```
$ git grep -n "new IDelegationCallerHost\|new IDelegationCommandHost" -- src
src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs:23:        { new IDelegationCallerHost with
src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs:44:        { new IDelegationCommandHost with
src/Wanxiangshu/Execution/Session/OpenCode/HorizonTool.fs:71:        { new IDelegationCallerHost with
```

Every member of those object expressions delegates to `ToolRuntimeScope` — the product's own runtime scope
(`src/Wanxiangshu/OpenCode/Tools/ToolRuntimeScope.fsi`): `scope.Journal` (`:95`), `scope.ViewForSession` (`:131`),
`scope.ManagerCapabilityFactsFor` (`:143`), `scope.EnsureRoleFor` (`:141`), `scope.OrchestratorHostFor` (`:188`),
`scope.RuntimeFor` (`:169`), `scope.LogicalOwnerFor` (`:133`). **Nothing on the two interfaces is supplied by the host
except `Caller`**, built by the adapter from the host payload (`HostToolContext`, `Fork/OpenCode/Tool.fs:26-33`, `HorizonTool.fs:74-81`).

### 2.3 Reference counts

```
$ git grep -o "IDelegationCommandHost" -- src | awk -F: '{print $1}' | sort | uniq -c
     66 src/Wanxiangshu/Execution/Delegation/Fork/Command.fs
      4 src/Wanxiangshu/Execution/Delegation/Fork/Command.fsi
      2 src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/Tool.fs
      1 src/Wanxiangshu/Host/DelegationCommand.fs
      1 src/Wanxiangshu/Host/DelegationCommand.fsi
```

74 occurrences in total: **70 in product code** (`Execution/Delegation/Fork/Command.fs` + `.fsi`), 2 in the adapter,
2 in the port's own declaration. (The card's draft said "65 of 68"; the measured numbers are 70 of 74.) `FissionHostPort`
is constructed at exactly one site — `src/Wanxiangshu/Execution/Fission/OpenCode/Tool.fs:64` — and consumed only by
`Execution/Fission/Launch.fs` and `Execution/Fission/Convergence.fs`; `PairGuidanceLedger` is implemented and
consumed entirely by product code (`src/Wanxiangshu/Interaction/PairGuidance/Occurrences.fs:95,141,155,186`), while
only `PairGuidanceTranscript` is built from a host payload
(`src/Wanxiangshu/OpenCode/Host/PairProgrammingThoughtTransform.fs:994-1000`).

### 2.4 P-18 is not on master and claims an id reserved elsewhere

`src/Wanxiangshu/Host/Tools.fs(i)` does not exist on `origin/master`; it lives on `origin/wip/hni-tools`. Its header
doc comment calls itself the **"P-04 tool contribution port"** while REGISTRY reserves P-18 for it — a reserved-id
collision gate ⑫ reports once the file moves under `Host/Ports/` (marker `HNI P-18`, base name not `Tools`, D-07).

### 2.5 The D-04 offenders, measured

```
$ git grep -n "Result<.*string" -- 'src/Wanxiangshu/Host/**'
```

13 lines, **seven distinct declarations** (each `.fsi` line has an `.fs` twin):

| declaration | file:line | nature |
|---|---|---|
| `ForkRuntime` | `Host/DelegationCaller.fs(i):25` | host round trip → product runtime |
| `OpenManagerJob` | `Host/DelegationCommand.fs(i):32` | host round trip → product orchestrator |
| `ContinueManagerJob` | `Host/DelegationCommand.fs(i):41` | host round trip → product orchestrator |
| `OwnerRuns` | `Host/FissionPort.fs(i):68` | host round trip → product run registry |
| `Carrier` | `Host/PairGuidance.fs(i):22` | decoded host fact, `string` error channel |
| `Append` | `Host/PairGuidance.fs(i):33` | product ledger write dressed as a host seam |
| `TaskDelivery.validate` | `Host/Facts.fsi:35` | **not** a port, **not** a round trip |

The first four are host round trips answering with the product's own runtime, orchestrator or run registry; the last is
a pure local witness validation, so §4 does not reach it — listed so the count is honest, not to widen it.

## 3. Per-port target

### 3.1 P-13 `CommandDeclaration` — stays tier B, shrinks to nothing else

`CommandDeclaration` / `CommandArgument` / `ArgumentShape` describe **what the product offers a participant**;
`ICommandArguments` is host-neutral read access to one call's arguments. All four go to
`Host/Domain/CommandDeclaration.fs(i)`, marker `HNI P-13`, namespace `Wanxiangshu.Host.Domain`; shapes unchanged.
`ICommandArguments` **is** a decoded host fact and stays: its only implementation is the OpenCode adapter
(`Execution/Delegation/Fork/OpenCode/Tool.fs:16-20`) reading `HostToolArguments`, a host payload. **Weakest fit,
one sentence:** `CommandDeclaration` describes what the product offers, not what a host supplies — a product artifact
traveling through the host's command registry. **What would move it out of tier B:** a host that cannot register
commands at all (no command-registration `HostCapability`), making the declaration purely product-side.

### 3.2 P-14 / P-15 — one product record plus one neutral record

```
// Host/Domain/DelegationCaller.fs(i)   (tier B — the host's contribution)
type DelegationCaller = { Session: SessionId option; ProviderRunId: ProviderRunIdentity option; ToolCallId: ToolCallId option }

// product: Execution/Delegation/Fork/DelegationEnvironment.fs(i)   (NOT under Host/)
type DelegationEnvironment =
    { Caller: DelegationCaller
      Journal: AgentJournal option
      ForkRuntime: unit -> Result<HostForkRuntime, HostError>
      LogicalOwnerFor: SessionId -> SessionId
      EnsureWorkDevOpsBound: SessionId -> Task<unit>
      WorkspaceDirectory: WorkspacePath option
      ViewForSession: SessionId -> Task<string option>
      ManagerCapabilityFactsFor: SessionId -> Task<ManagerCapabilityFacts>
      CallerRole: unit -> Task<Role option>
      NewHandleId: unit -> string
      OpenManagerJob: ManagerJobId * string * string * string * int option -> Task<HostResult<string>>
      ContinueManagerJob: ManagerJobId * string * ProviderRunIdentity * ToolCallId * int option -> Task<HostResult<string>> }
```

Ordered steps, each independently landable. **(a)** Move `Host/DelegationCaller.fs(i)` +
`Host/DelegationCommand.fs(i)` to `Host/Domain/`, keep both interfaces, add the `HNI P-14` / `HNI P-15` markers —
*the arch slice lands this half*. **(b)** Introduce `DelegationEnvironment` as a record **beside** the interfaces and
switch `ForkCommand` / `HorizonRoster` parameter types to it, with a product-side `ofHost` wrapper over the old
interface (one file, mechanical, no behaviour change). **(c)** Convert the `Result<_, string>` members to
`HostResult<_>` / `HostError` **while doing (b)** — `HostError.Refused of reason` carries the same prose and callers
already pattern-match on `Ok`/`Error`. **(d)** Delete the two interfaces from `Host/` and the adapter's
`new … with` bindings, leaving the adapter constructing only `DelegationCaller`. **(e)** Type `WorkspaceDirectory:
WorkspacePath option`; do **not** widen the `ViewForSession` / `ManagerCapabilityFactsFor` session ids to `SessionId`
in the same step — the adapter holds a `string` from the host payload, so that is a separate follow-up.
**Per-step acceptance** (each step): `node scripts/checks/architecture.mjs` prints `architecture: OK`; gates ⑪ and ⑫
still 0 (tier B is not under ⑪'s scope); `git grep -c "IDelegationCommandHost"` decreases monotonically; no
`Result<_, string>` member left in `Host/Domain/`.

### 3.3 P-16 `PairGuidance` — split by ownership

`PairGuidanceTranscript` (the decoded host fact) stays in `Host/Domain/PairGuidance.fs(i)` with the `HNI P-16`
marker; `PairGuidanceOccurrence` + `PairGuidanceLedger` (a product ledger; `Append` is product persistence) move to
`Interaction/PairGuidance/`. `Append`'s error becomes a **product error DU**, not `HostError`: it writes the product's
own durable journal, so it is not a host round trip, and `HostError` would misreport a product decision as a host
refusal; `render: … -> Result<'Wire, string>` in the same module is **product prose** and stays `string`.
Steps: (a) markers + move (arch slice); (b) the product moves the ledger types, the adapter keeps building only the
transcript. **Acceptance:** `git grep -n "PairGuidanceLedger" src | grep -v Interaction/` is empty.

### 3.4 P-17 `FissionPort` — shrinks to the two neutral ports

`FissionHostPort` keeps exactly `{ Sessions: ISessionPort; Events: ISignalSource option }` and is **deleted** in
favour of passing those two ports to `FissionLaunch` / `Convergence` directly when P-01/P-07 land — the record
exists today only because the neutral implementations do not. `DirectoryFor` is absorbed by **P-01**: `SessionInfo`
gains `Workspace: WorkspacePath option` (the host knows where a session runs) — a **P-01 shape change owned by the
fork/session lane**, not by this card, and **additive** (a new optional field; no existing member changes).
`WorkspaceDirectory` is absorbed by **P-10** `IHostPaths` (`ProjectRoot` / `StateRoot`, typed `WorkspacePath`).
`LogicalOwnerFor`, `OwnerRuns`, `RegisterLane`, `LaneStarted` are **product** and become composition inputs (the
same pattern as P-14 step (b)); `FissionOwnerRuns` and `FissionPtyCompletion` are product records too — the
product's own PTY/agent-run registry — and leave `Host/` with them. The transitional aliases `FissionSessionPort` /
`FissionEventPort` are deleted in the same change that lands P-01/P-07 (DESIGN §1 rule 8's transitional exception).
`FissionPtyCompletion.PtyId` / `OutstandingPtyRuns` stay `string`: **no typed `PtyId` is introduced** — a PTY id today
is minted by the product's own run registry, not by the host, so a typed id would be a product id in host costume.

### 3.5 P-18 `Tools` — merged into P-04/P-05, id voided

`HostToolContext` → P-04 `ToolInvocation` (it already carries `Session` / `CallId` / `Workspace` / `Args` /
`Cancellation` / `Progress`); `ToolAdmission` → `Admission: ToolAdmission option` on P-04 `ToolSpec` (admission is a
property of the contributed tool and the product decides it); `HostToolArguments` → P-04 `CanonicalJson` args with
a product-side accessor; `ToolResultText` (TOML prose rendering) → a product module — product prose, not a host
seam. **Deciding criterion (JUDGE): does any type in `Host/Tools.fs(i)` need a home that P-04/P-05 cannot give it?**
Answer: **no** — all three have homes. So **P-18 is voided** and the reserved id `P-18` is **retired, not reused**
(the file was never delivered to master; an undelivered child may settle its obligation by writing a void); the
branch's `Host/Tools.fs(i)` is deleted rather than renamed, and its "P-04 tool contribution port" header claim
disappears with it. **If** a future member cannot be folded into P-04/P-05, the fallback is a renamed family
`Host/Ports/ToolAdmission.fs(i)` with the `HNI P-18` marker — not the target recorded here.

## 4. D-04, restated precisely

Inside `src/Wanxiangshu/Host/**`, a **host round trip** uses `HostResult<_>` / `HostError`. A **parse verdict** may
keep `Result<_, unit>` — e.g. `ICommandArguments.OptionalCount` (`Host/CommandDeclaration.fsi:40`), a verdict about
one decoded argument, not a host round trip. `Result<_, string>` in **product** code is allowed and **not** in scope:
a product's own error prose is the product's. The seven declarations of §2.5 are the whole population under
`Host/**`; six are fixed by this card's steps, the seventh (`Host/Facts.fsi:35`) is out of scope by nature.

## 5. Status of D-02 / D-03 / D-04 after this record

**D-02 — tools lane redefined P-04 → resolved.** P-04 stays the registration port; `HostToolContext` folds into
`ToolInvocation`, `ToolAdmission` into `ToolSpec.Admission`, `ToolResultText` into a product module; P-18 is voided
and its id retired (§3.5). **D-03 — lane ports hand the domain product state "from the host" → resolved to a
contract.** P-14/P-15 become a product record (`DelegationEnvironment`) plus one neutral record
(`DelegationCaller`); P-16 splits transcript (host fact) from ledger (product); P-17 shrinks to the two neutral
ports; each owning lane now owns a numbered step (§3.2, §3.3, §3.4). **D-04 — `Result<_, string>` in lane ports →
resolved to a rule** (§4), the population listed with file:line in §2.5, the conversion happening in the steps of
§3.2/§3.3/§3.4. D-05 (namespace), D-06 (missing `HNI P-xx` markers), D-07 (file-name collision) are **not** closed
here: D-06 is closed *for the files this card touches*, D-07 is moot for P-18 because P-18 is voided, D-05 belongs
to the parallel arch slice.

## 6. What this record does NOT do

**No big-bang rewrite** — every step is independently landable and owned by one lane; the card's success criterion is
"closed or reduced to precise owner decisions", not "merged". **The owning lanes own their steps**: this card
decides *what* and *in what order*; the fork/session, fission, tools and arch lanes do the work in their own
commits. **No baselines are edited** — gate ⑩/⑫ baselines are untouched; a lane landing a step re-measures and only
lowers. **No compile or test evidence exists on this box** (owner's rule: the delivery team builds); the only
evidence is static — `git grep` / `git ls-tree` measurements and `node scripts/checks/architecture.mjs` printing
`architecture: OK`. **No files are moved by this record** — the `Host/Domain/` move is the arch slice's.

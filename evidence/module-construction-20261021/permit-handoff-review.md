# MF05 fixed-source handoff review

Read-only decision evidence. No fetch, source edit, package mutation, build, actual Host start, commit or push occurred. Current main finite probes used generation102. Upstream comparisons remain pinned to main `5880b824f52d6f7bab5ac36dfcb7eff9966fdc41` and library `202a41ab39460980ba053e55a9b3780bbaf411ca`; no later 117-commit range was incorporated.

## Status is a dated snapshot

The local BOARD has old broad BACKLOG rows for CUT10/11/12/17/25 and MF05 WAITING_HANDOFF. The construction plan says colleagues "still hold" those surfaces. Those statements must be weakened to dated upstream snapshot evidence. Root reports the app thread inventory did not locate real coworker work threads; this audit has no live coworker evidence and did not message anyone. Neither an old owner name nor DOING proves someone currently editing a worktree.

Fixed upstream main BOARD at 5880b824 (2026-10-21 05:46:29 +08) says:

| Card | Snapshot assertion | What actual fixed source supports |
|---|---|---|
| CUT10 | DOING, eng-cut10e-goose; borrowing remains | routing f982f8070, permit 9b4b64dad/0.33, capacity807a3db6a/0.35 and admission-queue57fbca343/0.37 subranges already integrated. ModelCapacity.Surface imports permit, Queue imports admission-queue; borrowing ledger/provider-step custody remains main. There is no CUT10 card file in this snapshot. BOARD's prose also retains an older "permit in progress" sentence, so its mixed history is not a fresh completion receipt. |
| CUT11 | DOING, eng-cut09e-goose; library first then main after CUT10 | Fixed library202a41ab is release0.40.0 dispatch, with ClaimLaw/Dispatch/index/AGENTS/tests. Fixed main Recovery still owns its own last50 user/key matching and Authority claim edits. No fixed main dispatch import and no CUT11 card file. Library rules are present, whole production intake is not demonstrated. |
| CUT12 | DOING, eng-cut12-goose; Accepted/Started/Terminal laws first | Fixed main ChatExecution.Fold remains local. Fixed library has Identity.ExecutionBinding but no independent ChatExecution single-assignment module/export/test family. `src/session` exports Ownership, which is resource scope ownership and must not be counted as CUT12 execution-session completion. No CUT12 card file. |
| CUT17 | DOING, eng-cut17e-goose | Its actual card has four bounded complete contracts: tenure filtering0.27, intent planning/layout0.34, retired-attempt request ownership0.39, each with main adapter and differential/requirement evidence. Whole-card DOING does not justify blocking review of those already-delivered subranges. No claim here that broader projection boundary is complete. |
| CUT25 | DOING, eng-cut25b-goose | Sandbox/WeakMap/await-timeout repair and admission unification are landed per card; fixed library also has programming projection. Card explicitly leaves prose/base class/capability-generator/Plan seams. Local sandbox package has already been separately repaired/received; do not wait for whole CUT25 to re-prove that slice, and do not infer remaining compiler/Plan work complete. |

Fixed library AGENTS provide scope contracts, not live work status. Authority explicitly leaves ExpectedClosureKind/source-witness matching (interaction-authority018) and run-scoped reset/fold to main. Dispatch explicitly does not hash, read journal, store projection or send Host messages. Identity explicitly leaves ExecutionBinding host intake to CUT10/12. Ownership is not execution-session.

## Selected boundary: exact permit release

Main producer and real evidence chain remains:

1. Dispatch.Send derives PromptKey from durable run/source/payload/sequence and records claim before send. Receipt alone only writes Submitted; a real physical id then permits PhysicalAccepted. AcceptanceUnknown leaves the claim pending. These are not permit messages.
2. ChatAdmission.Transaction accepts exact session/physical with canonical identity durably, then acquires capacity; ModelRouting.issueAdmission obtains the exact credit and issues the permit. Ordinary successful transaction is accept -> acquire -> read target -> project Host -> exact commit. A failed projection/commit compensates by durable pre-provider settlement before release.
3. ProviderLifecycle derives the exact physical request and frozen attempt, then records ProviderStarted from actual assistant/pre-provider snapshot evidence. A library commit response is not that durable evidence.
4. PluginSessionScope settles a durable terminal before exact physical release. ModelRouting's continuation-input retention, held physical releases, borrowed custody and ledger return decide when resources are actually returned/drained. Unknown physical results do not become completion through this pure package.

Relevant current paths: `Interaction/Dispatch/Send.fs` receipt branches150-205/255-310; `OpenCode/Host/ChatAdmission/Transaction.fs`172-187,436-474,510-544; `OpenCode/Host/ModelRouting.fs`804,1628,1303-1368,1800; `OpenCode/Host/ChatAdmission/ProviderLifecycle.fs`; `OpenCode/Host/PluginSessionScope.fs`95-120.

### Pure package versus current main

- Both implement one current permit per session and immutable exact identity(session,physical,role,participant,target) plus credit/fence; equivalent issue reuses the original handle, changed identity/credit supersedes the old handle.
- Both recognize only their own opaque instance-issued current handle with exact fence. Commit and pre-provider release require observed identity field equality.
- Both permit Pending->Committed->Released(PhysicalCompletion), Pending->Released(BeforeProvider), and Pending->Released(PhysicalCompletion) to retain a real end even if Started was not observed. Opposite terminal transitions conflict; same terminal repeats are idempotent.
- Current main Surface.fs holds lifecycleBySession and mint/recognition/transition logic; Model.fs additionally holds Lifecycle/Evidence/Decision and the transition law. Fixed upstream replaces that with library state plus a cache projecting the library permit back to F# lease handles. That cache must never store a second lifecycle.
- The package does not manage or release capacity credit, custody, queued task completion, physical send, durable receipt, Accepted/ProviderStarted/terminal facts, or continuation input retention. Those strong local contracts remain outside the package.

### Real benefit and limitation

There is no demonstrated current-main functional failure in this permit neighborhood. The benefit is a bounded independent owner: the same exact transition table, current-handle authority and stale/opposite-release behavior can be tested through an actual package, then deleted from both main Model and Surface. This is more than adding a predicate wrapper while retaining old state. It does not justify replacing ModelRouting, Queue, Ledger or Borrowing, nor claiming the whole chain repaired or received.

**Do not blindly copy fixed upstream's float bridge.** Main CapacityCreditId/CapacityLeaseId/CapacityFence are int64. Fixed permit accepts safe positive Number credit, generates Number lease/fence, and the upstream adapter converts main values with `float`. Distinct int64 values9007199254740992 and9007199254740993 map to the same Number, which the credit decoder then rejects as unsafe. This is a real domain mismatch in the proposed intake, not an observed current-main runaway counter and not a reason to rewrite the host. The package contract needs exact opaque credit representation and exact generated identities, or an explicitly approved narrower safe-number domain with fail-closed minting. Exact string/bigint representation best preserves the existing host domain. No product/library candidate has been implemented before resolving this contract.

`releasePhysical(session,physical)` intentionally does not accept a full lease. Its safety depends on the existing main rule that the same physical execution reuses identity/target/fence and a new execution has a new physical message. A package cannot independently prove a Host terminal really belongs to that physical execution. Preserve main exact physical proof, read-only committed lookup, and continuation retention guards.

## Proposed minimum construction package

First settle the exact wire representation above; then prepare an isolated library tree based on locally available c4bc2ca (verified in the integration submodule, not in the separate old upstream library checkout). Source slice: `src/permit/{ExecutionPermits.fs,Wanxiangli.Permit.fsproj,index.d.ts,AGENTS.md,tests/001.test.mjs}` plus narrow new boundary tests. Root alone owns public exports/version/lock/gitlink and all-package inventory changes.

Main source write set expected:

1. `OpenCode/Host/ModelCapacity/Surface.fs`: replace lifecycle state/transition/mint rules with the actual library owner; retain a typed opaque handle projection and shared transition counters only.
2. `OpenCode/Host/ModelCapacity/Model.fs` and `.fsi`: remove ExecutionCapacityLifecycle/Evidence/Decision and private release-state law after verifying no remaining callers; retain exact F# lease/credit/fence, transition outcome, failure and snapshot types needed by real consumers. If exact wire construction requires a narrow typed factory, make it explicit here.
3. `requirements/execution-model-routing/tests/012.test.mjs` and014 only for targeted public semantic boundaries; keep existing tests. Surface.fsi public messages can remain unchanged. No borrowing, model scheduler ABI, dispatch/authority fold, Plugin hooks, or global fixture write is needed for this first package.

Acceptance: isolated Node/Bun build and public actual tarball/TS consumer; same identity/changed each field/foreign-or-forged handle; both terminal paths/opposite duplicate; old physical A completion after B; copied/frozen DTOs; exact credit/fence at >2^53 and Int64 boundaries; no half-mutated state after rejected input. Main formal012/014 and transaction011/managed-chat-execution003 fixed order, provider-start proof, stale-A/new-B and continuation-held-release managed-session-lifecycle027 must remain green. Actual Host evidence belongs to a later explicitly scheduled formal integration pass, not this finite audit.

## Finite observations

`probe.mjs` ran on actual current main public ModelRoutingSurface without real Host or IO. Node22 and Bun each exited0 with:

- exact retry handle reuse; old A physical release stale after B supersedes; B stays committed and ledger unchanged; B physical release once then duplicate leaves zero entries;
- committed before-provider release conflicts and increments the shared conflict counter once, without releasing the ledger;
- pending wrong participant cannot release; exact release once then duplicate returns AlreadyApplied;
- a separate arithmetic observation demonstrates the Int64->Number mismatch. It is explicitly not an executed library permit result or current-main test failure.

Evidence: `node.log`, `bun.log`, `receipt-node.json`, `receipt-bun.json`. Four observation groups, not formal requirement counts, durable replay, actual Host receipt, or proof that borrowing unknown boundaries are closed.

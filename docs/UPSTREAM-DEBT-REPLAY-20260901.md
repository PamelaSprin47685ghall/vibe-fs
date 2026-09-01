# Upstream debt replay — 2026-09-01

## Scope and ancestry

- Replay branch: `codex/upstream-debt-replay-20260901`
- Initial audit base: `ff85615e9a8dc0c94447eb55960a72deb46ed9db`
- Final replay base: `1d4d810c7` (eight later boundary-signing commits included)
- Preserved source branch: `codex/upstream-debt-audit-20260901`
- Method: inspect the refactored owner/signature/project boundary first, then replay only residual semantics. No merge commit or blind cherry-pick is used.
- Final integration rule: fetch `upstream/master` again, reconcile any tail semantically, run the full validation ladder, then open one cumulative PR.

## Final upstream-tail reconciliation

Immediately before release validation, upstream advanced in two four-commit tails:

- `93c28fe71` signs the fission runtime boundary;
- `55ed50b56` signs the provider-attempt planner boundary;
- `c05948501` signs the managed-chat execution boundary;
- `8caee37ca` signs time and model-routing boundaries;
- `b21d0dc2f` signs Host and Sphinx runtime boundaries;
- `b97f06006` signs OpenCode and degeneration boundaries;
- `fbf24db46` signs the cognitive prompt boundary;
- `1d4d810c7` signs the session ontology boundary.

These commits add or tighten `.fsi` files, owner project membership, and exact published contracts. They do not touch P2 reconciliation, P5 Judge, the A0 analyzer implementation, or the durable-tail proof. `scripts/checks/published-contracts.json` is the sole path changed by both histories: upstream adds contracts for its new signatures; A0 adds exact declarations for pre-existing FCS uses exposed by earlier signatures. The cumulative branch was rebased node-by-node first onto `8caee37ca`, then onto `1d4d810c7`; Git applied all 19 replay and release-fix nodes without conflict, preserving both disjoint contract sets. No upstream production code was reverted or compatibility layer introduced.

## Module ledger

| Module | Old source nodes | Latest-upstream finding | Replay nodes | State |
| --- | --- | --- | --- | --- |
| P2 causal reconciliation | `a6445f214`, `3f481417a`, `b855d9891` | Compiler owner projects were added, but `Reread`, counter parameters, recursive snapshot reads, and the test-local mirror remained. | `ffb846594`, `38276e4ef`, `744578f6e` | GREEN; closure recorded here |
| P5 Judge decision ownership | `6b93b752d`, `f3b2372d1`, `2b405f573` | The owner project existed, but `JudgeTool` used a private decision while `JudgeSurface.validateContext` implemented a disconnected approximation. | `e3f3ea840`, `18f1b1f14`, `e9fbc9fa8` | GREEN; closure recorded here |
| A0 owner graph | `5ab743901`, `975762df3`, `1ca0a8be5`, `cd1878ce4`, `af4de1d79` | Compiler projects now enforce the locality DAG, but exact FCS semantic-edge snapshots, deltas, and derived manifest counts remained absent. | `df013fcc4`, `cd1e8e4b1`, `99344ecbd`, `58958f04f`, `8841fd018`, `af8aaed99` | GREEN; closure recorded here |
| P6B evidence/property work | `3c2581088`, `bfd81120e`, `e22f07982`, `c6b10c89e` | Failure/capacity finite proofs remain stronger than random sampling; durable incomplete-tail space remains uncovered. | `e07d24afb`, `cf992a381` | GREEN; two NO-GO decisions and one GO recorded here |

## P2 — causal edges are the only snapshot-read authority

### Upstream state changed

The replay intentionally changes existing upstream production and proof files. The refactor had placed the four production files into these compiler localities:

- `Composition/Turn/Program.fs` → `dispatch-protocol/execution-session-recovery-model`
- `Composition/Turn/ReconcilePass.fs` → `dispatch-protocol/interaction-dispatch-opencode-ingresscodec`
- `Composition/Turn/ReconcileSurface.fs` → `dispatch-protocol/composition-turn-reconcilesurface`
- `Composition/Turn/Scheduler.fs` → `managed-session-lifecycle/opencode-host-pluginruntimescope`

Those boundaries compiled, but the implementation still exposed a `Reread` decision and counter-shaped API. `ReconcilePass` still contained recursive reread/error bookkeeping, while `Scheduler` forced the budgets to zero. The result happened to read once in production but kept a second, illegal mechanism in the model and public proof surface.

### RED — `ffb846594`

Added one production-bound counterexample for `HOST-BOUNDARY-005`:

- a real `Reconciler.Scheduler` pass without a projection edge must read the snapshot exactly once;
- that pass must hand the observed provisional turn to the business boundary;
- `ReconcileSurface.decideStep` must accept only wake + evidence and expose only `{ name }`.

Observed before production change:

```text
node --test requirements/host-boundary/tests/reconcile-idle-early.test.mjs
9 pass, 1 fail
TypeError: ReconcileSurface.idleProvisionalWithoutProjectionEdgeScenario is not a function
```

### GREEN — `38276e4ef`

- Deleted `ReconcileDecision.Reread` and its candidate/counter helpers.
- Reduced `decideStep` from `(wake, rereadsRemaining, evidence)` to `(wake, evidence)`.
- Removed recursive reread, consecutive-error retry, candidate accumulation, and compatibility constructor options.
- Made one `materializeActive` call perform at most one `GetMessages` read.
- Added the controlled production Scheduler scenario used by the RED proof.
- Deleted `reconcile-idle-observation-non-authoritative.test.mjs`, which implemented its own JavaScript reconciliation algorithm instead of calling production.
- Updated all affected tests and HOW linkage to the narrower production surface.

### Proof and gates

```text
node scripts/build.mjs
build ok; 739 F# outputs; 165 registered JS surfaces; 772 linked modules

focused reconciliation suite
38/38 pass

node scripts/checks/owner-projects.mjs
148 localities; 701 sources; 1771 refs; DAG

node scripts/check.mjs
all gates pass; 773 WHAT; 3909 executable tests; closure complete
```

The proof is mutation-sensitive at the production boundary: reintroducing the old three-argument surface, returning counter fields, performing a second read without another projection/host edge, or failing to deliver the provisional observation breaks a committed assertion.

## P5 — Judge Tool and proof share one decision owner

### Upstream state changed

The replay intentionally changes existing upstream `JudgeTool.fs`, `JudgeSurface.fs`, and `verdict-tool-extras.test.mjs`. Upstream already grouped Tool + Surface in the `review-judgement/mission-review-opencode-judgetool` owner project, but compiler colocation alone did not establish semantic identity:

- `JudgeTool.execute` called a private decision over live runtime objects;
- `JudgeSurface.validateContext` separately modeled role/session/tree booleans that the Tool did not consume;
- four tests proved only that mirror and could stay green if the real Tool decision regressed.

### RED — `e3f3ea840`

Added production-Surface counterexamples for the complete execution evidence:

- `PERFECT` and `REVISE` with exact identities proceed;
- non-Reviewer, missing session/tool call/provider run, and blank physical identity are typed refusals;
- a prior submission blocks only the same physical review request;
- a targeted mutant bypassing only blank-identity rejection is killed.

Observed before production change: `4 pass, 3 fail`; all three new tests failed because `JudgeSurface.decideExecution` did not exist.

### GREEN — `18f1b1f14`

- Extracted `ExecutionEvidence → ExecutionDecision` as the single pure decision in `JudgeTool`.
- Replaced raw prose paths in the decision with `ExecutionRejection` cases and one rejection-to-resource mapping.
- Made live Tool execution build evidence and consume that decision.
- Made `JudgeSurface.decideExecution` convert JS values to the same evidence and convert the same decision back to plain JS.
- Deleted `validateContext` and its four mirror-only tests.
- Preserved exact-request dedupe by requiring a nonblank physical identity before `AlreadyJudged` can be produced.

### Proof and gates

```text
node scripts/build.mjs
build ok; JudgeTool.fs and JudgeSurface.fs compile in the refactored owner project

focused shared-decision + contract + finality suite
14/14 pass

real Tool execution suite after npm ci
8/8 pass

node scripts/check.mjs
all gates pass; 773 WHAT; 3912 executable tests; closure complete
```

The first combined run also found a local environment mismatch: upstream had added locked `jq-wasm@3.0.0-jq-1.8.2`, but the pre-fetch `node_modules` did not contain it. `npm ci` restored the exact lockfile dependency; the unchanged real Tool suite then passed 8/8. No dependency manifest or upstream production logic was changed for this environment repair.

## A0 — exact semantic graph facts beside the compiler DAG

### What the upstream refactor superseded

Upstream's `owner-projects.mjs` and 148 owner-locality projects now provide the stronger structural law: every production source belongs to one locality; foreign project references are explicit; contract closure excludes runtime/private sources; the compiler project graph is a DAG. A0 does not recreate that responsibility.

The retained residual law belongs to the FCS semantic-use oracle, which remains required by AGENTS until every exact symbol/consumer promise can be checked without white-box evidence. It answers a different review question: “Which semantic owner edges changed in this PR, and what exact SCC does the current source graph contain?”

### RED/GREEN sequence

- `df013fcc4` RED: 39 tests, 36 pass, 3 fail for absent exact SCC facts, absent added/removed edge comparison, and the handwritten `semantic-owners.json.total`.
- `cd1e8e4b1` GREEN: derive the owner count; produce a versioned, sorted, duplicate-free graph with exact SCC members/internal edges; compare exact edge keys. Result: 39/39.
- `99344ecbd` RED: 40 tests, 39 pass, 1 fail because no-baseline JSON could not be reused directly as a baseline.
- `58958f04f` GREEN: emit the bare snapshot without a baseline and `{ current, delta }` only for a comparison. Result: 40/40.

### Real-repository canary and upstream refactor drift

The first real `--graph-json` scan proved the CLI shape but exited nonzero on 11 pre-existing upstream metadata violations. None came from A0 analysis logic:

- 10 exact FCS uses had signed `.fsi/fsproj` boundaries but incomplete `published-contracts.json` entries;
- `OwnerIdentityWitness` still named the removed `let internal inherited ...` issuer after upstream commit `3c585e24a` replaced it with `PromptIdentitySeed.inheritFromOwner`.

`8841fd018` fixes only those metadata declarations:

- add `durable-events` to the existing Change projection contract;
- publish the existing `ReviewRequirementProjection` type to its existing `durable-events` consumer;
- publish `HostToolFactory` and `ToolSpec` to the three Tool owners whose `.fsi` signatures already expose those exact types;
- update the authority issuer symbol/anchor to the current production declaration.

No production logic, wildcard, baseline, suppression, runtime/private symbol, or project edge was added.

### Proof and gates

```text
owner-dependencies
OK — 29002 FCS cross-owner uses; 0 pending; 778 contracts
49 owners; 621 unique semantic edges; 1 justified semantic SCC

owner-projects
OK — 148 localities; 701 sources; 1771 refs; compiler DAG

graph replay canary
bare snapshot: 49 owners / 621 edges
same-snapshot comparison: +0 / -0

authority-boundary
OK

composition-root-invariant
OK

semantic-decorator-invariant
OK
```

The 46-owner semantic SCC does not contradict the 148-locality compiler DAG. The former reports owner-level semantic use across published contracts; the latter is the physical compile-reference topology that must remain acyclic. Conflating them would either hide semantic coupling or weaken compiler enforcement.

## P6B — property testing only where it adds a new oracle

The current owners and proofs were re-audited instead of replaying the old documentation verbatim. Detailed evidence is in `docs/FAST-CHECK-GO-NO-GO.md`.

### P6B-2 execution failure — NO-GO

The latest upstream already contains the 21-test closed-algebra suite, the 216-case deterministic cross-boundary matrix, and three exact mutants. The core domain is finite and enumerable. A fast-check generator would either sample less than the existing table or reproduce the recovery state machine. No repository change is justified solely to adopt the tool.

### P6B-3 durable writer tails — GO (`e07d24afb`)

The latest WHAT still forbids every incomplete NDJSON tail and skip-corrupt continuation, while no property covered canonical payload × arbitrary cut position. The replay adds:

- 300 fixed-seed production-encoded event streams;
- arbitrary non-empty suffix truncation by UTF-8 byte, including cuts inside multibyte characters;
- the real `.git/wanxiang/events` physical layout and retained-writer reader;
- a skip-corrupt-tail mutant that must fail and replay from its shrink path;
- direct HOW links for DURABLE-EVENTS-004/007.

Focused result: 2/2 in about 0.18s. No production code, Surface, or dependency changed.

### P6B-4 capacity — NO-GO

The latest upstream still has the exact lifecycle table, wrong-identity decoys, stale-newer counterexample, 3,808-operation admission soak, 832-operation lineage soak, 16-process restart soak, and exhaustive 720 causal orderings. The focused bundle passes 20/20. A stateful generator would need a second queue/fence ownership model and its shrinking would primarily expose invalid test schedules. The existing deterministic proofs remain the smaller and stronger description.

### Intentional limitation

The 216-case failure matrix contains dimensions that do not affect the policy input and a small mirrored effect mapping. It remains mutation-sensitive and production-bound, but its count is not claimed as 216 distinct policy worlds. This is documented debt for the next modification of that owner, not a reason to broaden this upstream replay.

## Release-sink findings in upstream code

The first complete `npm run format-build-test` exposed failures that existed on the initial release base `upstream/master@8caee37ca`; none were introduced by P2, P5, A0, or P6B. They are changed in this PR because the upstream release entry cannot otherwise complete. Each correction is isolated from the replay modules and remains necessary on `1d4d810c7`.

### Verification gates — `663466799`

1. `ctx-capacity-observation-forbidden.test.mjs` scanned every production file for the generic word `tokenizer`. It rejected `Execution/Session/LoopDetector.fs`, although that file is owned by `degeneration-guard` and uses tokenization to detect repetition—not to observe model context capacity. Deleting or renaming that production dependency would damage a valid owner to satisfy an over-broad oracle. The corrected proof obtains exact `context-compression` files from `semantic-owners.json` and applies the same forbidden vocabulary only to the owner governed by CONTEXT-COMPRESSION-001. Empty ownership fails closed.
2. `proof-ladder.test.mjs` required `owner-dependencies.mjs` to be textually adjacent to `semantic-owners.mjs`. Upstream's valid `owner-projects.mjs` DAG gate now sits between them. Evidence causality requires only `semantic-owners < owner-dependencies < every FCS consumer`; the corrected assertion states that order without rejecting an independent intervening gate.
3. `scripts/checks/js-module-linkage.mjs` is executed by `scripts/build.mjs` after compilation, beside `js-surface-manifest.mjs`, but the proof-ladder inventory omitted it from the build-owned allowlist. The corrected inventory names the already-wired gate; it does not suppress or bypass it.

RED from the first release sink: 3861/3865 unit tests, with these three code failures plus one sandbox-only localhost EPERM. GREEN focused run with localhost permission: CTX + proof ladder + installed OpenCode canary, 15/15. The canary proved the EPERM was environmental; no Host assertion was changed.

### Isolated compiler tool home — `01d93700e`

`run-inner.mjs` intentionally replaces `HOME`/`USERPROFILE` so tests cannot read developer OpenCode configuration. That also hid the .NET local-tool store from child compiler canaries. Direct `owner-project-compiler-boundary.test.mjs` passed, while the same test under the integration inner runner failed before compilation with “Run dotnet tool restore”.

The fix freezes `DOTNET_CLI_HOME` from the caller before replacing application `HOME`. OpenCode remains isolated; only the dedicated .NET tool location survives. The exact `run-inner.mjs → owner-project-compiler-boundary.test.mjs` path then passed all nine positive/negative Fable project cases in 27.7s. The final complete integration repeated the same proof in 27.3s.

### Mandatory formatting — `8016a1d00`

The repository release entry runs Fantomas before every other layer. Its first pass normalized 107 `.fsi` files and three `.fs` files, including signatures present before and added by the upstream boundary-signing tail. The changes are layout-only and were isolated into one style commit; the next pass reported 978 files unchanged. Fable compiled the normalized tree successfully. This commit intentionally modifies upstream-authored files because otherwise every release run regenerates the same dirty tree.

## Final validation

One uninterrupted, localhost-enabled `npm run format-build-test` completed with exit 0 after all fixes:

```text
format/check: clean; 773 WHAT; 3914 executable proof declarations
owner dependency scan: 29330 exact FCS cross-owner uses; 0 pending; 778 contracts
build: 1069 F# source inputs; 165 registered surfaces; 772 emitted modules linked
unit: 3865 passed, 0 failed
integration: every registered suite passed, including the real owner-project compiler boundary
package integration: all suites passed
installed OpenCode warmup/canary: 1.18.18
Long Stroke e2e: passed
npm pack --dry-run: 2020 files; 2.2 MB packed; 10.6 MB unpacked
```

No baseline, suppression, allowlist for semantic debt, compatibility facade, test retry, timeout increase, or production bypass was introduced.

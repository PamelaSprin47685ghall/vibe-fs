# Upstream debt replay — 2026-09-01

## Scope and ancestry

- Replay branch: `codex/upstream-debt-replay-20260901`
- Frozen upstream base: `ff85615e9a8dc0c94447eb55960a72deb46ed9db`
- Preserved source branch: `codex/upstream-debt-audit-20260901`
- Method: inspect the refactored owner/signature/project boundary first, then replay only residual semantics. No merge commit or blind cherry-pick is used.
- Final integration rule: fetch `upstream/master` again, reconcile any tail semantically, run the full validation ladder, then open one cumulative PR.

## Module ledger

| Module | Old source nodes | Latest-upstream finding | Replay nodes | State |
| --- | --- | --- | --- | --- |
| P2 causal reconciliation | `a6445f214`, `3f481417a`, `b855d9891` | Compiler owner projects were added, but `Reread`, counter parameters, recursive snapshot reads, and the test-local mirror remained. | `0ae4a2686`, `e51dade3f` | GREEN; closure recorded here |
| P5 Judge decision ownership | `6b93b752d`, `f3b2372d1`, `2b405f573` | The owner project existed, but `JudgeTool` used a private decision while `JudgeSurface.validateContext` implemented a disconnected approximation. | `be83da934`, `58c6cc429` | GREEN; closure recorded here |
| A0 owner graph | `5ab743901`, `975762df3`, `1ca0a8be5`, `cd1878ce4`, `af4de1d79` | Compiler projects now enforce the locality DAG, but exact FCS semantic-edge snapshots, deltas, and derived manifest counts remained absent. | `a52fccc3d`, `249865007`, `0b4681b72`, `3f786d29a` | GREEN; closure recorded here |
| P6B evidence/property work | `3c2581088`, `bfd81120e`, `e22f07982`, `c6b10c89e` | Pending re-audit against current execution-failure, capacity, and durable-event owners. | — | Pending |

## P2 — causal edges are the only snapshot-read authority

### Upstream state changed

The replay intentionally changes existing upstream production and proof files. The refactor had placed the four production files into these compiler localities:

- `Composition/Turn/Program.fs` → `dispatch-protocol/execution-session-recovery-model`
- `Composition/Turn/ReconcilePass.fs` → `dispatch-protocol/interaction-dispatch-opencode-ingresscodec`
- `Composition/Turn/ReconcileSurface.fs` → `dispatch-protocol/composition-turn-reconcilesurface`
- `Composition/Turn/Scheduler.fs` → `managed-session-lifecycle/opencode-host-pluginruntimescope`

Those boundaries compiled, but the implementation still exposed a `Reread` decision and counter-shaped API. `ReconcilePass` still contained recursive reread/error bookkeeping, while `Scheduler` forced the budgets to zero. The result happened to read once in production but kept a second, illegal mechanism in the model and public proof surface.

### RED — `0ae4a2686`

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

### GREEN — `e51dade3f`

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

### RED — `be83da934`

Added production-Surface counterexamples for the complete execution evidence:

- `PERFECT` and `REVISE` with exact identities proceed;
- non-Reviewer, missing session/tool call/provider run, and blank physical identity are typed refusals;
- a prior submission blocks only the same physical review request;
- a targeted mutant bypassing only blank-identity rejection is killed.

Observed before production change: `4 pass, 3 fail`; all three new tests failed because `JudgeSurface.decideExecution` did not exist.

### GREEN — `58c6cc429`

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

- `a52fccc3d` RED: 39 tests, 36 pass, 3 fail for absent exact SCC facts, absent added/removed edge comparison, and the handwritten `semantic-owners.json.total`.
- `249865007` GREEN: derive the owner count; produce a versioned, sorted, duplicate-free graph with exact SCC members/internal edges; compare exact edge keys. Result: 39/39.
- `0b4681b72` RED: 40 tests, 39 pass, 1 fail because no-baseline JSON could not be reused directly as a baseline.
- `3f786d29a` GREEN: emit the bare snapshot without a baseline and `{ current, delta }` only for a comparison. Result: 40/40.

### Real-repository canary and upstream refactor drift

The first real `--graph-json` scan proved the CLI shape but exited nonzero on 11 pre-existing upstream metadata violations. None came from A0 analysis logic:

- 10 exact FCS uses had signed `.fsi/fsproj` boundaries but incomplete `published-contracts.json` entries;
- `OwnerIdentityWitness` still named the removed `let internal inherited ...` issuer after upstream commit `3c585e24a` replaced it with `PromptIdentitySeed.inheritFromOwner`.

`4749912f7` fixes only those metadata declarations:

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

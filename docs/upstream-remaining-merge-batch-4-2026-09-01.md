# Upstream Remaining Merge — Batch 4

## Result

Batch 4 closes M1, the JS transaction correctness module, against `upstream/master@fcd5ab11b`.

Upstream review: [PR #22](https://github.com/PamelaSprin47685ghall/vibe-fs/pull/22).

The change does not replay the pre-refactor patch. It maps the three retained requirements to the refactored owners:

- `REPOSITORY-PROGRAMMING-014` → immutable read evidence and exact commit preflight.
- `REPOSITORY-PROGRAMMING-013` → canonical typed commit plan and all-or-nothing physical application.
- `REPOSITORY-PROGRAMMING-015` → content-CAS rollback that preserves third-party writes.

## Upstream behavior changed

The upstream implementation was modified in these places:

1. `JsToolsBindings` now records `{ Path; Text }` for successful `file()` reads and every file scanned by `grep()`. A create target that already exists is rejected before staging.
2. `JsToolWorkflow` passes those immutable snapshots through the single `JsTransaction.preflight` decision, checks before and after the access observation boundary, and checks again after durable Prepare.
3. `JsTransaction` replaces path/text tuples with `JsCommitMutation` and `JsRollbackMutation`. Commit order is canonical by path. Each rewrite carries its expected old content; each rollback carries both the restore value and the exact value written by this transaction.
4. `JsMutationFs` validates every target before the first effect, revalidates each rewrite immediately before writing, creates files with Node's exclusive `wx` flag, and unwinds applied writes only while disk still equals this transaction's value.
5. The registered JS surfaces translate the typed plans without exposing F# union layout. `runObserved` exposes the existing access-observation boundary so executable proofs can create deterministic external races through the production workflow.

These changes are required because the previous adapter re-snapshotted targets at commit time and used that later value as the rollback baseline. An external edit could therefore be overwritten during commit or normal rollback even though the WHAT requires `FILE_CHANGED` and preservation of third-party content.

## Proof design

The RED checkpoint is `e57d880b0`. Against unmodified upstream it produced ten failures, including a missing surface export, wrong plan shapes, accepted existing creates, and missing conflict decisions.

The GREEN checkpoint is `9b2e8493a`. Proofs call registered production surfaces and distinguish these worlds:

- read-only dependency unchanged → commit; changed → `FILE_CHANGED`, zero output file;
- every file scanned by `grep()` unchanged → commit; changed → `FILE_CHANGED`;
- create target absent → exclusive create; present at physical commit → `FILE_CHANGED`, external content preserved;
- rewrite baseline exact → write; stale at physical commit → `FILE_CHANGED`, external content preserved;
- rollback target still equals transaction output → restore/remove; third-party changed it → leave it untouched;
- inverse declaration order → canonical path-order plan, so ordering is asserted rather than accidental.

The rollback proof first asserts that the production commit succeeded and all three new values reached disk. This prevents a no-op or malformed-plan implementation from making the later rollback assertions pass vacuously.

## Verification

- `node scripts/build.mjs` — PASS; 734 F# sources, 161 registered surfaces.
- `node scripts/check.mjs` — PASS; 36 gates, 0 F# control-pyramid debt, 772 WHAT links and 3926 executable declarations closed.
- `node --test requirements/repository-programming/tests/*.test.mjs` — PASS; 111/111.
- Focused transaction/binding/filesystem/workflow suite — PASS; 48/48.
- Formatting — PASS; 696 F# files checked.

No baseline, suppression, allowlist, threshold, timeout, or assertion was weakened.

### Linux production-evidence follow-up

The first cumulative Linux run exposed one defect introduced by this PR, not inherited from upstream: `JsMutationFs.writeCreate` received `resolvePath` as an operation and could invoke it again while classifying a failed exclusive create. The production FCS gate rejected that unowned repeated trace.

`847850587` removes the repeated operation instead of declaring an artificial retry contract. `commitPlan` and `rollbackPlan` now resolve every logical path exactly once into private typed mutations. Preflight, immediate revalidation, write, failure classification, and CAS rollback reuse that exact resolved path. External error paths remain logical paths; no host path leaks through the public failure algebra.

Follow-up proof:

- `node scripts/build.mjs` — PASS; 734 F# sources, 161 registered surfaces.
- Transaction/filesystem/adapter proofs — PASS; 13/13.
- `owner-dependencies-reuse.test.mjs` — PASS; the real production FCS scan and fail-closed evidence reuse completed in 107.5s.
- `npm run format` — PASS; 696 F# files, one formatted.

This repair adds no semantic-decorator declaration, suppression, allowance, timeout, or duplicated path formula.

## Remaining work

This PR closes only batch 4 / M1. Batches 5–9 remain separate modules and PRs. `upstream/master` was fetched again immediately before PR creation and remained `fcd5ab11b`; no additional semantic merge was required.

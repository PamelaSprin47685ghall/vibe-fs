# Upstream Remaining Merge — Batch 8

## Result

Batch 8 closes M7D and M7E on cumulative batch-7 head `1b437db25`. Every consumer of `requirements/managed-session-lifecycle/tests/support/managed-surface.mjs` now calls a registered production surface, so the mirror file is deleted.

## Production boundaries added or extended

- `SyncDelegateSurface` adds three narrow operations on its existing opaque runtime: stage the exact deleted Inspector, inspect the scope-close child, and cancel one session. All state transitions remain in `SyncDelegateRuntime`, `SyncDelegateCallStore` and `AttachedSessionRuntime`.
- `HostForkPtySurface` owns a controlled `PtyPort` backend around the real `HostForkRuntimePty`. It records backend effects; command validation, ownership, tracking, lookup, signal parsing, LF normalization, read selection and error propagation remain production decisions.

## Proof correction

The deleted SyncDelegate mirror returned one constant object with four booleans. Its replacement creates the real journal-backed runtime, admits the HumanRoot owner, accepts exact physical prompts and observes:

- one child reused across two completed calls;
- deleted Inspector removed from live binding yet retained for scope close;
- the next call creates a replacement child;
- owner cancel fails the pending call without creating another child;
- runtime disposal fails every unsettled invocation.

The deleted PTY mirror decided blank validation, ownership, signal/write/read and errors inside JavaScript. Its replacement executes eleven cases through `HostForkRuntimePty`, including spawn exception rollback, unowned/closed rejection, `INT → SIGINT`, newline completion, parked read resolution and backend failure propagation.

## History

- `b680f729f` — RED: require production SyncDelegate lifecycle controls.
- `ad5d58437` — GREEN: expose narrow opaque-runtime controls; 5/5 focused.
- `8b32a54fe` — RED: require the missing HostFork PTY surface.
- `11485ff83` — GREEN: execute eleven PTY cases through production.
- `5c3eaa9e0` — closure: delete the final mirror support file and close GAP-031.

## Verification

- Fable build: PASS; 738 sources, 165 registered surfaces.
- managed-session-lifecycle: PASS; 124/124.
- `node scripts/check.mjs`: PASS; 700 production files owned, 36 ledger nodes DONE, zero control-pyramid/deadcode/JS-boundary debt, 772 WHAT / 3899 tests traced.
- Full `npm run format-build-test`: pending final batch-exit run.

No baseline, suppression, allowlist, threshold or timeout was changed. No production behavior was weakened to preserve an old test.

## Remaining work

M7A–M7E are closed. Batch 9 upgrades requirement trace and Surface Manifest to a shared AST binding analyzer. The `MANAGED-SESSION-006` retirement contradiction recorded in batch 7 remains an explicit owner decision outside this mirror-removal scope.

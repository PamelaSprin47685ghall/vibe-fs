# Independent read-only owner/bridge audit

Target: main integration worktree generation 100, wanxiangli 0.9.2. No source edits, builds, physical-host tests, commits, or pushes in this audit.

## Result

No new blocking production regression was reproduced. Eight isolated probe groups exit 0 on Node v22.13.0 and Bun 1.3.14. These are adversarial in-process probes, not requirement-runner or native lifecycle acceptance counts.

- Exact seal copies keys, rejects the wrong call, ignores same-fence replay, preserves captured bytes, and lets only its run consume. Late Anchor replacement and late prose do not change main sealed handoff or the returned cursor.
- Restored material preserves exact whitespace, Unicode, NUL and line bytes. Handoff invents no material keys.
- Every final checkpoint failure remains pending under later committed/WriterUnavailable/failure settlement and both completed/error observations. WriterUnavailable releases admission; a new completed observation can commit and every later terminal skips.
- Prefix decimal wire preserves 9007199254740992, 9007199254740993 and Int64 max-1 through proposal, commit and stale/repeated evidence rejection. No numeric coercion is used in production Epoch.fs.
- PluginHooks registers the shared Promise before starting append through Promise.resolve().then(start). Its success/release remove Task and admission in one synchronous continuation; terminal Unknown retains both. Actual physical journal failure/concurrent-consumer acceptance remains owned by existing obligation-ledger005 and release03, not newly rerun here.

## Reproduced nonblocking library boundary

A fence-mode unsealed ticket claimed before seal can still handOff candidates and prose after the same cell is sealed. The old ticket retains its snapshot. The sealed cell remains the original captured material, and that ticket has Advance=false, so it does not advance the returned cursor. Main's claim -> select -> handOff -> seal sequence has no await and does not expose the ticket, so the audit found no production trigger. Treat as a P3 API lifetime clarification/testing opportunity, not a reason to weaken or expand the production bridge during this release.

## Release03 delegation031 fixture failure

031.test.mjs settle helper uses sync.child immediately before recordFencedDeliveredView. abandonPendingCall invokes CancelSession; Surface.child subsequently returns null because Runtime.TryFind has no live binding. Registering a null session is invalid and is unrelated to an actual late child's delivery. The test should capture/assert the real child before abandonment, register late evidence on that captured child, and call real sync.settle afterward to assert false plus failed pending result. Surface.settle already returns false when waitForReadyCall finds no live call. No production consumer requiring a null session was found.

## Probe integrity

First harness run had one false failure: key.includes('late') matched the probe session name 'isolated-native-audit' inside a prose key. The original log and receipt are preserved. The final assertion names the actual forbidden late replacement/prose bytes; no product behavior or baseline was changed.

Known prefix reverse/unprocessed-candidate and Proxy reflection-throw boundaries were intentionally not retested or counted as new findings.

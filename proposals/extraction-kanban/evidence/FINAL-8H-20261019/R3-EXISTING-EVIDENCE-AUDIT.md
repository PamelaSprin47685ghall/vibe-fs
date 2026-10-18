# CUT-00-R3: existing evidence versus R1 supervision changes

Independent read-only audit, d1_review_git, 2026-10-19. No execution, source change, package installation or new reproduction. R12-U1 was not investigated.

## Conclusion

The first isolated 6cc27 PTY failure still has no established internal cause. The retained evidence does establish a real nonzero native test result and the identity of at least one failing leaf; those facts should not be called wholly unknown. R1 supplies no demonstrated causal explanation for that old failure. Later same-package green results do not close R3 or explain the first red.

## What is established

1. Original log `/private/tmp/process-r7-20261018-main-isolated.log:53` records the command `/opt/homebrew/Cellar/node/26.6.0/bin/node --test requirements/managed-session-lifecycle/tests/025.test.mjs`. Lines60-61 record status1 and signal null. The preserved invocation is native node --test, not the formal supervise-node-test/run-inner entry.
2. Line113 (duplicated in the error's stdout property at191) records `local late-not-started preserves its exact evidence kind (bun)` failing after9378.149416ms. It is a Node test file running the Bun helper case. It is not evidence that a formal supervisor killed this leaf at5s.
3. Line216 explicitly ends a printed string with `... 9680 more characters`; line217 has empty stderr. The retained log contains no AssertionError, ERR_ASSERTION, expected value or complete authoritative test summary. Console error-object output truncated the captured inner stdout. The missing assertion/child diagnostic cannot be recovered from the retained duplicate prefix. This known reporting loss does not explain why the inner test returned nonzero.
4. The retained package is exact SHA256 `6cc27a69236fa29da2012fbec29ae448d278da27eb859bfb69ad327b5fda7762`. `/private/tmp/cut-00-r3-evidence-5eB2A7/summary.json` records a later independent full native run:124/124, exit0, no timeout,2076 installed files unchanged. Its command/result JSON and complete stdout are retained. The earlier targeted same-case log records a separate1/1. Neither is the original failing execution.

## Why R1 does not yet explain R3

- R1's proven old5s silence-window failures were formal-wrapper runs (FINITE-PHYSICAL-WAIT-R1-2026-10-19.md:21). The original R3 command above did not use that wrapper. Its child actually returned status1; the available record does not show an outer timeout signal.
- R1's686 stdout/native-frame issue has observed late dequeue/ACK evidence for that specific formal runner (same report:23-24). No original R3 dequeue stream, ACK failure or corresponding parser observation was preserved. A general resemblance involving Node output is not a causal link.
- R1's689/700 end-ACK/next-begin race used the new private control socket, pending token and allowance state (report:50-54). Those are later validation facilities, not the old R3 invocation.702's green cannot retroactively explain the old red.
- The retained package-proof script now shows native direct invocation with180000ms outer timeout at66-70, but it was subsequently extended with large-only and targeted-PTY modes. The old error stack points to line62. Its current SHA is recorded below as a retained later script, not asserted to be the first-run source snapshot.

The current `pty-local-evidence.mjs` contains bounded waits and several exact assertions. Its Git history has only the earlier266f0c8eb change, so it provides useful future probe anchors, but9378ms alone does not identify which assertion or physical observation failed. It is not sufficient to assert an8s terminal wait failure, a product Unknown bug, resource pressure or an R1 parser cause.

## Remaining precise unknown

Unknown is the first failing helper's actual diagnostic/assertion, its exact terminal/request/owner evidence and the contribution of prior cases/runtime/environment. It is not unknown whether the outer native test returned nonzero or whether the named Bun case was red. We must not infer123/1, pretend the entire124-case run passed, or classify the failure as a confirmed R7 product regression without its missing cause.

Keep CUT-00-R3 READY and C/R7-A REVIEW per REMAINING-REPAIRS-2026-10-16.md:176-183. Future work should follow that finite plan and save full inner stdout/stderr, status, exact fixture/input identity and physical cleanup before any exception is printed. The present read-only audit adds no new dynamic evidence and does not expand that plan or reopen R12-U1.

## Original evidence hashes

- process-r7-20261018-main-isolated.log: f4faf6d418109a4c0634fc640387d144d30920d683d2d3333e5a70a76ca561a2
- Retained later process-r7-main-package-proof.mjs:435d5e4d4c6d9d03bce1394586a03c680c1daf34fdb106f4569060e258b1cbd5
- Later full rerun summary.json:8bf55cccd9ce032355fc1ebd4e69d7a2ca0d53a887c3d6fa9a4196310f8e1732
- Exact old product tarball:6cc27a69236fa29da2012fbec29ae448d278da27eb859bfb69ad327b5fda7762

Repository summary anchors: PROCESS-OUTPUT-EXTENT-2026-10-18.md:46; PROCESS-BACKPRESSURE-2026-10-18.md:58; FINITE-PHYSICAL-WAIT-R1-2026-10-19.md:21,23,46,50-56. No later green is substituted for the original failure.

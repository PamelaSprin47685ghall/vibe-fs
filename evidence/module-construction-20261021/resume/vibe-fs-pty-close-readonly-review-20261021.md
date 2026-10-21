# PTY control close: bounded read-only review

Read only. No source change, build, signal, socket connection or test was performed for this review. D remains unimplemented. This review uses the original gen106 long-run artifacts and current F# source; the copied compiled client/worker was also checked directly so Fable closure/reference behavior is not inferred only from source.

## Finding

The original experiment has a real public recovery-availability failure after the deliberately paused custodian and timed-out recovery client. It is not a failure of the two-hour interaction, complete observed output/input hash, real EOF or exact target exit23. It also is not a normal terminal close pass: no authenticated terminal receipt was obtained and fallback cleanup killed the retained scope. Root's shorter boundary experiment can distinguish the unproved initial-detach premise without repeating two hours.

The current evidence does not identify which authenticated socket still occupied the worker's controller slot. Therefore it does not yet prove a particular worker close-event defect or a Bun runtime defect. It is incorrect to treat the text “live control connection” as proof that the underlying peer was still connected: that guard checks only controller != null/undefined. Keep the guard; do not replace it with a timer or blindly clear another socket's authority.

## Exact observed order

Original harness `/private/tmp/vibe-fs-pty-soak-gen106-20261021-b/consumer/pty-soak.mjs:198-235`:

1. target exit23 is saved, scope-ended is absent and the descendant is still alive.
2. creator.dispose() requests destruction of its local socket; harness waits 100ms, then stops only the custodian.
3. a new recover client sends an authenticated attach, while the custodian cannot run. It times out after 5018.47ms with startup acknowledgement timeout. Recovery finally disposes that client. No terminal receipt exists.
4. custodian resumes. Invalid-secret probes observe live-controller rejection for about 15 seconds; fallback public recovery also rejects. Original harness exits1.

events.jsonl records creator's local close-derived fault at 04:05:51.842Z, before the stopped interval. This proves that local client close handling ran; it does not prove the worker already ran its peer close callback. There is no remote detach acknowledgement. The last resources.jsonl sample is 04:05:51.835Z, before dispose/STOP, with numeric FD counts only. No failed-stage socket identity/end/close trace exists. sandbox_audit confirmed that limitation.

## Worker authority and legal states

`PtyRecovery.fs:428-456` checks sealed, then existing controller, then credentials/mode/creator admission before assigning controller. All invalid-secret probes fail before assignment. Their rejected handler calls exact-reference detach, so they cannot steal or clear an unrelated current controller. Repeating those probes does not itself explain a stuck owner.

`accept:490-509` releases controller on the exact socket's close event, or through rejected/error handling. It does not observe end explicitly. `command:479` checks the socket receiving the new frame, not the already-stored controller socket's native state. Thus “live” rejection proves only a retained authenticated reference. The compiled gen106 code preserves exact identity comparison and assignment; no Fable capture bug was found by reading it.

Two legal transient states fit the observed ordering:

- Old creator destruction has completed locally, but its peer EOF/close has not been processed by the custodian. A 100ms sleep is not a remote detach barrier. New recovery attach must still be rejected while that owner is genuinely admitted.
- After creator detach, an already-sent recovery attach can be processed only after CONT, even though its caller timed out and destroyed its local socket while the custodian was stopped. Worker can temporarily admit that authenticated, now-withdrawn connection before processing its EOF/error/close. The worker may have a readable-ended peer while its write half/queued acknowledgements have not yet closed. Since only close/error releases controller, it can retain the slot during that interval.

The second is a plausible leading explanation because creator's local close was observed before STOP and the timed-out recover attach was specifically queued while stopped. It is an inference, not identification: the original logs cannot exclude the first. No assumption about a Bun implementation bug is needed to explain the possible states. Under the intended disconnect lifecycle, either temporary state should eventually detach through the exact close/error path; continuous rejection and failed fallback show that intended recovery did not complete in this run, without revealing which event/state failed to converge.

## Client close semantics

`createClient.fail:563-567` records the first failure, destroys the local socket and delivers faults. drainClient:832-844 disposes in finally after the awaited failure. `dispose:765-770` clears its poll timer and calls socket.destroy; it returns no Promise and does not await a local or remote close acknowledgement. Calling it twice does not supply new detach evidence. `attached` and `started` are historical acknowledgements, so inspect retaining true after a connection-lost failure does not prove a still-live creator.

Worker `socket.end()` in rejection requests write-side finish; it is not equivalent to its close callback having fired. Conversely, client destroy is a local teardown request, not evidence that a paused remote worker has released controller. Receiving request-side EOF can establish that no more control frames will arrive on that exact connection, but output draining/physical scope completion remain separate. Any future close fix must preserve complete ordered output for a live creator and must not manufacture a terminal receipt.

There is a separate static client edge: dispose does not mark the asynchronous connect task disposed, so an awaitEndpoint continuation could open a socket after early disposal when endpoint availability was delayed. This was not the observed original run (available existed for two hours and client initialization could proceed), so it is recorded only, not asserted as this failure's cause or expanded into a new task.

## Decision and narrow follow-up

Record the original as failed final recovery with retained ownership and successful independent cleanup. Do not relabel it as a normal close, and do not infer live scope completion from target exit23 or a stop request. Do not erase or relax the live-controller guard. Current source has a close-observation/remote-detach availability boundary worth checking, but this read-only evidence cannot establish the exact defective callback.

The already-authorized short separation is appropriate: prove real credentials-mismatch response after creator disposal before STOP, then run the same timed-out recovery and CONT. Failure before that boundary points to creator detach; passing it and then failing after CONT points to the withdrawn recover attachment. It is a new bounded diagnosis, not a retry that can turn the original long-run exit1 into pass. No further matrix, instrumentation or implementation is requested by this report.

### Later independent bounded diagnosis

After the read-only analysis above, sandbox_audit reported its single root-authorized short separation ended in 23.08s with exit1. Its existing events were then read, without running another experiment: `/private/tmp/vibe-fs-pty-detach-diagnostic-20261021/events.jsonl`. After creator.dispose and 100ms, the first invalid-secret probe actually returned credentials mismatch at 04:11:08.746Z in about 1.10ms. Worker therefore had no controller at that admission point. Only then was custodian stopped; a recover client timed out in about 5020ms; after CONT the same 15-second live-controller refusal recurred. All 833 gen106 production artifacts were unchanged, according to the independent diagnostic owner.

This narrows the functional defect in that new case to the timed-out recover attachment/worker cleanup boundary and excludes an old creator that never detached as the necessary explanation. The raw failed socket's end/close/native state is still unrecorded, so it does not identify an exact missing event or justify blaming the runtime. The original two-hour run remains failed, with its original attribution limits; the shorter case is separate stronger evidence for the production recovery availability defect. Cleanup is being verified by sandbox_audit, not presumed here.

Root's delivery decision is P2: record this fault-recovery timing boundary and leave it unfixed in this batch, following the user's instruction to prioritize the working flow and record smaller issues. No further matrix or implementation follows this review. The short run belongs to sandbox_audit; this agent only read its saved evidence. Main source and the public library remain unchanged.

## Sources and immutable inputs

- Current F# `/Users/yuanxi/.codex/worktrees/upstream-plan-20261020/vibe-fs/src/Wanxiangshu/Process/PtyRecovery.fs`: SHA256 060429d9652bc36157eb83fd08a2d8fe412d1e51d7e5e19df7d7b041eee2f1c0.
- Original consumed gen106 `/private/tmp/vibe-fs-pty-soak-gen106-20261021-b/consumer/dist/Process/PtyRecovery.js`: 0c1964c0342f82a23b3d22b2953f19d0265a9162de50fcc642efc22db1e944e8.
- Original harness: 4511c919d41538aa77fd6ebd0276151252dfb95127e8833a0eaf64c6d100b32f.
- Original status/events/resources and final-receipt.json reside in that same experiment root. cleanup-receipt.json records all seven owned PIDs and PID FDs absent after fallback cleanup, the removed stale endpoint and preserved native scope files. Those cleanup facts do not retroactively prove normal terminal settlement.

# MF01 isolated candidate A — work in progress

The Host fs closures operate on real scratch files only. They do not implement or prove production path authorization, read snapshots, Prepared/Committed journal ordering, ReadOutput rechecks, SDK capability checks, or final ledger settlement. No main or public-library source/metadata was changed. This is not MF01 reception.

Candidate source directory: `/private/tmp/vibe-fs-mf01-vertical-a-20261021`. Five hand-edited source files: owner.mjs, guest.mjs, wire.mjs, host-apis.mjs, candidate.test.mjs.

## Retained first results, not acceptance of the latest unrun revision

- Gate1 first version: Node22 4/0 and Bun1.3.14 4/0. The first Node successful transfer disposed by KILL after the result; this did not establish natural exit zero. Logs: `/private/tmp/vibe-fs-mf01-a-gate1-{node,bun}-first.log`.
- The same gate1 cases then gained a stronger natural Closed/exit-zero assertion. Node22 and Bun each 4/0, with actual sync FIFO Host write/parallel async cat, full large Unicode replies, accepted/lost-reply mutation once, responsive Host during a blocked sync read, and after-await runaway killed by the external watchdog. Logs: `/private/tmp/vibe-fs-mf01-a-gate1-{node,bun}-close.log`.
- Initial gate2 Node22 2/0. Initial Bun 1/1: the large stopped-process resume retained original closure 41 to 42, immediate sync write, whole large payload and natural exit zero, but an auxiliary assertion wrongly required the stopped process to survive SIGTERM. Bun actually reached confirmed Closed/SIGTERM. Original failure: `/private/tmp/vibe-fs-mf01-a-gate2-bun-first.log`; it is retained as a fixture-premise error, not a reason to relabel confirmed closure as unknown.
- Root directed an explicit trusted TERM-ignoring readiness premise, then STOP/T observation, TERM/remaining stopped, KILL/EOF/ESRCH. That revised gate2 passed Node22 2/0 in `/private/tmp/vibe-fs-mf01-a-gate2-node-term-resistant.log`. Bun revalidation was deferred when main release08 started. Old tests are not performance baselines.
- Each returned owner in these runs passed physical Closed, null physical failure and PID ESRCH cleanup assertions. All 14 explicitly rechecked logged PIDs were absent in a final native ps query; ordinary sandbox ps permission failure was not counted as absence.

## Static repairs since the last run

These are prepared, syntax-checked and not yet runtime-verified:

- Startup failure/ready timeout cleanup retains its original error and either proves cleanup or returns an AggregateError carrying the still-owned cleanup controller. FIFO creation moved under that guarded path.
- Capture-failure close wait returning TimedOut now retains ClosingUnknown and private descriptor/controller; it never sets Closed or resolves null. A later real close observation is required before descriptor release. An injection test wraps an actual ProcessSession and suppresses its close receipt, distinct from a real OS failure claim.
- Exact one-shot park site identity is now required to resume. A consumed old site cannot resume a later park generation. Host control-ack handling enters the appropriate authority state before sending activate; guest does not resolve its deferred until activate arrives. Late activation cannot reopen a closing owner.
- Added prepared cases for pre-park accepted async work settling while stopped and later receiving its original reply, and again-park retaining one continuation with stale-site rejection.
- Wire refuses non-finite, cyclic, function/symbol/bigint, accessor and non-plain data; undefined and absence remain different and UTF8 split boundaries remain explicit. These checks are candidate transport behavior, not a proof of every production JsFailure mapping.

## Fixed actual dependency inputs

Actual consumed bundles were copied byte-for-byte to local `.mjs` files so later edits/rebuilds to temporary upstream trees cannot alter the candidate runtime:

- `dependencies/ProcessSession.mjs`: SHA256 `57ecdb37c1c7c00cca0b735853fb563c85b42efd9ca3290261a332cd0f199054`. Origin `/private/tmp/wxl-routing-protocol-20261021/dist/process/ProcessSession.js`, published library source HEAD45849f5. Source ProcessSession.fs hash `1daa8686af3227f0ceb53b18f47298e9c99f2a07bf50b232ebaa67b91aacbe37`; Native.fs `0f5282645a4eec055fe089c71b706d4f02c52bc4740947ba67668e5f496bb488`; native BunProcess.fs `38b447437c4569d4c9af4dd23787d5ba8c9de0853b25fdb2f255e8a5fb5d4233`.
- `dependencies/Sandbox.mjs`: SHA256 `9157f06c7b4ca850c1e5f0ba709db4dcd0ad1e0c0ab9e92bdf34c192e0952eec`. Origin `/private/tmp/wanxiangli-module-sandbox-20261021/dist/sandbox/Sandbox.js`; corresponding source hash `e8750dc9a7439e16084da329222215796e3e4a95b0e8ced40f75f003846c8497`.

This legacy membrane is inside the guest process; its pause only stops its internal pump. Actual Parked publication still requires OS T observation by the external owner. The membrane's known async limitations are not claimed fixed. Its 60000ms synchronous invocation cap also remains an explicit candidate limitation: no long borrow/reset/release contract has been implemented or verified. It must not silently become the production deadline authority.

## Next permitted sequence

Wait for root's main release08 completion before further deadline/CPU/physical pause experiments. Revalidate the two gates and prepared boundary cases sequentially on Node22 and Bun, retain any new first failure, and freeze the exact seven file hashes for independent testing. Only after the two gates and owner failure boundaries pass may this package extend to the guest-local Callable port and real scratch Host finally. Sanity thunk results and their argument arrays must stay guest-local; the Host must not JSON-coerce proof arguments or receive executable guest objects.

## Current gate/owner freeze after release08

Root released the sensitive-run reservation after main release08/gen107 completed. The revised ten tests then ran in the required Node22 to Bun order, without main build or other sensitive trials in parallel:

- Node22.13.0: 10 pass / 0 fail / 0 skip / 0 TODO, exit 0. `/private/tmp/vibe-fs-mf01-a-gates-boundary-node-first.log`.
- Bun1.3.14: 10 pass / 0 fail, no filtered cases. `/private/tmp/vibe-fs-mf01-a-gates-boundary-bun-first.log`. The combined Bun-test plus hash-check command exited 0; its subordinate exit was not separately written to a receipt file, so the Bun 10/0 summary and combined status are the retained evidence.
- All seven actual input files match the pre-run SHA256 manifest after both runs: `/private/tmp/vibe-fs-mf01-a-gates-boundary-inputs.sha256`, `/private/tmp/vibe-fs-mf01-a-gates-boundary-inputs-after.log`.
- Frozen owner hash `85c26dff60cb466a308cede9b7789964448ad9595f9b0571a7293559dac49c6a`; guest `28c220730240351b9936c63ecf66b6a4fd707073738920065e0faa6b765ccda7`; wire `c449d595cb73265ef43329b5974bf92a214e49f17b35c5961cc5b28b15e5f5b3`; host APIs `6eece0af19c33cf92bbe408cfe166139823ec609755df699614e7bf63c61318b`; tests `0fe1fcec75c8b820763d8319082df405df67c9ffabdab27dc54f0e323c473d23`. Dependency hashes remain the two fixed values above.
- The cases include explicit ready-proven TERM resistance, retained accepted async reply during physical T, again-park exact-site rejection, startup failure/private-FIFO ownership, and injected unknown close receipt with descriptor retained until the actual close receipt becomes observable. No branch accepts arbitrary terminal results to make the earlier Bun fixture failure pass.
- Returned normal owners print actual PIDs, native exit and transport faults; each corresponding finally asserts actual Closed, null physical failure and PID ESRCH. Startup/unknown-boundary cases additionally assert their direct session/descriptor cleanup in code; they are error-observation injection and startup tests, not broad OS crash or total FD-budget proofs.

Independent tester delivery_audit has the frozen source, fixed input manifest and permission to build separate finite counterexamples in another temporary file. Candidate source remains unchanged while that independent review runs. Callable/finally have not been expanded, and the fixed 60000ms membrane limit, full deadline borrow semantics, production ledger/capability integration and memory cap remain open.

## Independent cancellation counterexample and narrow repair

The independent extra set found one real outcome bug on both runtimes: an actual complete result frame was buffered while the child was Running and present; the exact Host owner entered Closing before dispatch, yet dispatch published Result. Original logs remain separate. The seven original inputs were copied unchanged to `/private/tmp/vibe-fs-mf01-vertical-a-base-red-20261021`; all seven hashes match the old freeze above.

Only owner outcome admission and two terminal tests changed. Host admission of a complete passive result/failure is the single business linearization point. Active/Abandoning can settle once, entering ProgramSettled; Closing, ClosingUnknown or Closed cannot admit a late business success or reopen through activation. Abandoning always publishes Abandoned, even when model code catches the rejection and returns normally. Actual native close remains separately observed; already accepted Host work stays under its original owner.

- The unchanged original real-Closing counterexample now passes Node22 and Bun, each 1 pass / 0 fail, separate exit 0: `/private/tmp/vibe-fs-mf01-a-closing-node-fixed.log` and `...-bun-fixed.log`. Node closed by SIGKILL; Bun naturally exited 0 after cancellation won Host admission. Native signal outcome does not define business cancellation.
- Two new terminal cases pass both runtimes, 2/0 each, exit 0: `/private/tmp/vibe-fs-mf01-a-terminal-node-first.log` and `...-bun-first.log`. Duplicate/late injected frames are explicitly a narrow state-gate check; the independent cancellation proof uses a real frame from a real child.
- Complete finite A set: Node22 12/0/0skip/0TODO, exit 0; Bun 12/0, exit 0. Logs `/private/tmp/vibe-fs-mf01-a-terminal-full-node-first.log` and `...-bun-first.log`. Each returned owner again asserts Closed, null physical failure and ESRCH; startup and retained-close injections preserve their previous strong assertions.
- New frozen manifest `/private/tmp/vibe-fs-mf01-a-terminal-inputs.sha256`: owner `66ce95326e007a0d147f81c058892f0a11b669a34e7bacbbe3bed918f88b2c30`, tests `04ae0da0f3740568d4c56cc49e5a595e32d23691e75da492bbdde28bec3c03f8`. Other five files are unchanged. No production source or public package changed.

The independent test's earlier Result-with-abandoned-marker expectation describes the old A outcome shape. Its author will update that expectation to the explicit Abandoned contract for the next independent run; no old result or raw evidence is overwritten here. Callable work is prepared only in a separate directory and has not yet run.

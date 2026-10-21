# C isolated deadline candidate

This package proves a temporary Host deadline mechanism around a real guest process and scratch filesystem. It has no production shell, ledger, ReadOutput, full Callable diagnostics or memory enforcement. It inherits B's confirmed production-reception blocker: generic passive projection rejects valid class command objects and invokes toJSON where the old Host reads fields without invoking it. C passing does not make B or C production-ready. Main, public library/API and metadata were not changed.

## Source and build

The frozen design is `/private/tmp/vibe-fs-mf01-c-deadline-design-20261021.md`. B's nine inputs were copied unchanged before edits. Hand edits affect only local owner.mjs and guest.mjs; host-deadline.mjs and deadline.test.mjs are new. The realm, wire, scratch API, copied A10/B16 tests and original ProcessSession/Sandbox bundles remain byte-identical to B.

The membrane was derived from real F# source, not edited generated JavaScript. Baseline source hash e8750dc9a7439e16084da329222215796e3e4a95b0e8ced40f75f003846c8497 corresponds to B's fixed Sandbox bundle. A local minimal Fable-only fsproj and the copied unchanged library build script compile only src/guest-membrane/Sandbox.fs with Fable compiler JS and esbuild. The actual output was copied byte-for-byte to dependencies/GuestMembrane.mjs. First build passed, exit 0, no warnings in `/private/tmp/vibe-fs-mf01-c-membrane-build01.log`. Its input source/build snapshot and manifest are `...-build01-inputs.tar.gz` and `...-build01-inputs.sha256`.

Initial dist/guest-membrane/Sandbox.js and imported GuestMembrane.mjs hashed to 0c7d67b301c65de4e6e66cd836a95bd994e71a79ea3135898d29886ac2ad4393. After the final comment-only F# correction, normal FableJS/esbuild build02 passed exit 0; actual final bundle hashes to 24502759a59c6d38e25cdce96fb766dd134c0f5e0e83ed856d228036a0110767. `/private/tmp/vibe-fs-mf01-c-membrane-build02-continuity.log` confirms raw bytes differ, but normalization of only the generated `.build/guest-membrane-PID/` comment yields exact equality. No generated code was edited. Both source/build snapshots and the first bundle are preserved. Toolchain receipt `/private/tmp/vibe-fs-mf01-c-toolchain.log` records FableJS3.8.1, esbuild0.28.2, fable-library2.8.1 and actual compiler/loader/package hashes. The development dependency link is build-only; consumed runtime bundles are fixed local files.

The F# diff removes guest business epoch checks and model timeout options from initial evaluation, callback invocation and microtask pumping. Realm/context/membrane/error mapping remain. Trusted static installation retains a separate 5000ms setup bound and Host startup supervision; this is not a proof that startup is included within a complete production program maximum. No epoch is sent to the guest on start or resume, and the guest has no deadline reset API. The 61-second async control does not prove a 61-second single synchronous invocation; removal of the old 60000ms invocation cap is source/diff evidence plus finite external cutoff tests.

## Single owner semantics

HostDeadline is private to the exact GuestOwner. First borrow freezes the original remaining base budget; live leases use absolute ends; intermediate updates take the maximum of now plus that saved budget and live ends. Last release restores now plus that one snapshot. Reset with live leases replaces the snapshot without cancelling the live lease. Release is idempotent. Timer delays are chunked to the supported range and every timer/admission reads the same current epoch.

Past reset then future reset in one Host turn remains legal until an expiry timer or actual business admission commits Closing, matching production repository-programming028. After Closing there is no revival. RPC command, actual guest request, callback reply and complete business outcome admission all read the same owner. Already accepted Host operations remain owned and drain even if their guest closes. Outcome settlement and native close remain separate A/B facts.

Park disarms the same watchdog only after exact identity and actual T, while lease absolute ends keep aging. Parked allows trusted releases/reset but rejects new borrowing/capabilities. Exact unpark resets the owner and arms it before CONT; existing ack -> admission -> activate controls the same original continuation. Cleanup is an explicitly approved new candidate contract: enterCleanup irreversibly fixes that same owner's deadline and retires old execution leases. Cleanup capabilities remain usable; old release/new cleanup borrow/reset cannot extend it. Retiring leases does not remove already accepted Host task ownership.

## Preserved negative evidence

Two baseline versions and their complete actual source snapshots are retained separately. The initial baseline assertion awaited top-level completion while its offer had not been finished, so its counterfactual success path was incomplete. It still observed a real second-authority failure, but it is not the final fair regression. Original Node/Bun logs each 0/1 exit 1 remain `...-c-legacy-{node,bun}-base-red.log`; source snapshot `...-c-legacy-red-inputs.tar.gz` is retained.

Corrected baseline explicitly awaits the original callback, then would finish the offer and require normal completion. Both runtimes fail that callback with PROGRAM_SETTLED after GuestFailure/ERR_SCRIPT_EXECUTION_TIMEOUT while Host still has about 1300ms and its watchdog has not fired. Each 0/1 exit 1: `/private/tmp/vibe-fs-mf01-c-legacy-node-base-red02.log`, `...-bun-base-red02.log`. Exact code/dependency snapshot `...-c-legacy-red02-inputs.tar.gz` permits replay. These tests run the frozen old B module through its actual guest, not a hand-copied fake algorithm; only its existing Host arm is extended, without an epoch message.

## Current runtime evidence

The first C run accidentally omitted Node's explicit file concurrency limit. It passed 42/0/0skip/0TODO, exit 0, 68.54s, but the copied A/B files could overlap the beginning of the C file. It is kept separately as a non-authoritative timing round: `/private/tmp/vibe-fs-mf01-c-node-round01.log`, exact source/dependency archive `...-c-round01-inputs.tar.gz` and manifest `...-c-round01-inputs.sha256`. No source was changed after this round.

The same inputs then passed strict serial Node22 using --test-concurrency=1: 42/0/0skip/0TODO, independent exit 0, 74.56s. Log `/private/tmp/vibe-fs-mf01-c-node-round02-serial.log`, archive `...-c-round02-inputs.tar.gz`, manifest `...-c-round02-inputs.sha256`; all 15 inputs match after this run in `...-c-node-round02-inputs-after.log`. This includes the new C16, copied A10 and B16. Node's 61-second callback elapsed 61057.07ms, restored 1496ms of base budget and exited normally after the actual scratch write and finish. The separate exhaustion control restored 347ms and physically closed after 349.76ms.

### First Bun framework failure and exact cleanup

Bun's first C file run was 15 pass / 1 fail, exit 1: `/private/tmp/vibe-fs-mf01-c-bun-deadline-first.log`. Its default per-test 5000ms runner timeout terminated the 61-second case before the candidate deadline, reporting a dangling process. The original round02 source archive/manifest and raw failure remain; this is a fixture timeout, not a Host timeout result. No successful cleanup assertion was inferred from the runner message.

Only one owned residue remained: `/private/var/folders/g_/3ydzn7kx5lg9ylq8fd9p_13r0000gn/T/mf01-deadline-gQrA5G`, containing the known seed and private FIFO. Native ps and exact C Bun guest pgrep showed no actual guest process. lsof on that exact FIFO showed no descriptor holder. Its tree was archived before unlink/removal; before/after receipt and archive are `/private/tmp/vibe-fs-mf01-c-bun-timeout-residue-before.log`, `...-after.log`, `...-residue.tar.gz`. The first failed test had not printed its original PID, so no old-PID ESRCH assertion is claimed. Final helper now prints exact PID/run/directory/FIFO immediately after opening, before the wait.

### Final fixture and exact evidence split

The new 61-second test now explicitly declares a finite 70000ms test-runner bound, actually accepted by Node/Bun's test(name, options, callback) interface. Business lease remains 66000ms, its callback wait remains 65000ms and other case bounds did not change. CPU-entry polling was changed from a non-cancelled inner loop under Promise.race to a loop with its own monotonic end and an explicit entry assertion. The only F# change after Node round02 is the header comment repair, with equivalent rebuilt output as above. Owner/guest/deadline policy and all B/A code are unchanged.

- Final-source affected Node CPU case: 1/0/0skip/0TODO, independent exit 0, `/private/tmp/vibe-fs-mf01-c-node-cpu-round03.log`. It proves the repaired fixture and real external update/cutoff. The 61-second Node case was not needlessly repeated; complete Node42 remains tied to the preserved prior fixture/source/bundle and is not relabelled a final-source full rerun.
- Final-source Bun C16: 16/0, independent exit 0, 68.21s, `/private/tmp/vibe-fs-mf01-c-bun-deadline-round03.log`. The actual long callback elapsed 61060.54ms, then wrote real bytes, restored its 1494ms base budget and normally closed. Original remaining-budget exhaustion restored 344ms and physically closed after 349.57ms.
- Final-source Bun copied A10: 10/0, independent exit 0, `/private/tmp/vibe-fs-mf01-c-bun-gates-round03.log`.
- Final-source Bun copied B16: 16/0, independent exit 0, `/private/tmp/vibe-fs-mf01-c-bun-callable-round03.log`.

All final runs were sequential, with no main build or other sensitive experiment in parallel. All 15 C source/dependency inputs match their saved round03 manifest after completion: `/private/tmp/vibe-fs-mf01-c-round03-inputs-after.log`. C16 prints each exact early owner, later native exit, watchdog and fault facts; cleanup asserts Closed, null native failure, ESRCH, drained accepted tasks and removed FIFO/closed private descriptor. Copied A/B retain their existing physical cleanup assertions, including startup and retained-close observation injections. The real buffered request/result controls observed native Running/PID present before expiry, then published TimedOut; Bun's result case naturally exited 0 after expiry won admission, which did not restore business success.

The frozen final source snapshot also includes the original B files used by deadline.test's baseline-only LegacyOwner import, rather than relying on hashes for unavailable external code. Their original B hashes remain unchanged. The final all-input manifest/archive are `/private/tmp/vibe-fs-mf01-c-final-inputs.sha256` and `/private/tmp/vibe-fs-mf01-c-final-source.tar.gz`. Runtime source is now frozen for independent C testing. No still-running owned experiment remains.

## Known open boundaries

- B's two actual command-compatibility counterexamples remain unmodified. Generic projection is not a production Callable decoder.
- Full production deadline/borrow computation, admin hard limit, shell cancellation/physical drains, ledger and ReadOutput are absent. C offers trusted Host borrow/reset operations only.
- Trusted installer/startup is separately bounded; no complete production max-duration claim.
- The current wall-clock epoch contract is retained. Elapsed witness measurements are monotonic, but wall-clock jumps and hard real-time scheduling are not proved.
- nextOffer currently shifts a queued offer before checking terminal phase. Later callable admission still rejects, but the terminal owner API boundary remains for independent review; no automatic scope expansion or production-impact claim.
- The retained vm pump can keep evaluating while awaiting work; no CPU-efficiency or production scaling claim is made from the 61-second asynchronous model operation.
- Memory cap, public STOP/CONT ownership API, complete isolation and whole production reception remain unclosed.

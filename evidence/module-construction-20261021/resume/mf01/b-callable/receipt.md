# Isolated Callable candidate

This is a temporary RPC mechanism candidate with real scratch Host filesystem operations. It has no production shell process, ledger, ReadOutput admission, borrowed deadline owner or capability integration. It does not close MF01. The original A candidate and its base-red snapshot are separate and unchanged by this work.

## Inputs and write boundary

Copied the original frozen A five source files and two exact dependency bundles. Only `owner.mjs` and `guest.mjs` changed; `callable-realm.mjs` and `callable.test.mjs` were added. `wire.mjs`, `host-apis.mjs`, `candidate.test.mjs` and both bundled dependencies remain copied byte-for-byte. The A outcome repair is mirrored in B owner: business admission occurs once at full passive-frame dispatch, only while Active or Abandoning; closing states reject late outcome frames. Program settlement and native close are separate facts.

No main source, public library source, package metadata, build graph or shared test fixture changed. No model function is reconstructed from source. The trusted registry source is static code in the guest realm; wrapped local dispatch only connects that realm to the bootstrap inside the same physical guest.

## RPC and exact ownership

Class, instance, sanity pair, thunk, proof argument array and lexical state remain in the guest registry. Host gets passive opaque references bound to a non-reused run incarnation. Host reference identity is retained in a WeakSet; copying a reference object is rejected before transport. The guest independently checks incarnation, kind, live entry and owning offer. A proof thunk binds its exact proof instance and method. Host separately orders construction, metadata read, proof and fresh execution construction.

The sanity thunk array is tested only with Array.isArray and directly applied to the same guest proof instance using captured Reflect.apply. Its function and undefined members never cross the wire. Metadata type validation happens before a passive DTO is returned. Missing/function/Proxy metadata is not JSON-coerced into numeric null. Normal output getters, thenables and toJSON execute inside the guest before strict passive conversion; invalid values reject.

Proof next accepts a nullish return. Watch accepts false or an own abort field with a non-nullish value, including a getter evaluated only in guest; prototype abort fields are not vetoes. This candidate returns the minimal argumentArray/accepted verdict. The full production sanity diagnostic payload, its returned-value diagnostics and park/fail policy have not been ported or claimed preserved.

Command IDs are single-use. Release supports an exact instance reference and revokes its thunks. Finish consumes its exact offer before transfer; its reply is emitted before a trusted local completion resolves the original offer await. Lost replies are never authorization to replay a method or finish, and stale finish cannot settle a successor offer. Candidate-only dropped-reply options supply explicit test observation faults.

The existing control-ack to Host admission to activate sequence remains: no legal continuation is resolved until Host entered Active/Abandoning. Abandon rejects original outstanding offers and parked await, retains the bounded cleanup phase, refuses another park, and forces Abandoned business outcome even if model code catches and returns. Native close is separately required; actual scratch write/rm in finally is the positive control. A fixed legacy membrane 60000ms invocation limit is still open, as are production borrow/reset/release and full JsFailure coverage.

## First runtime evidence and freeze

All four changed/added files passed syntax checks; the embedded trusted realm source was also compiled without execution. After root returned the physical reservation, runs were sequential Node22 then Bun, with no main build or other sensitive experiment in parallel.

The 16 finite Callable cases passed on the first run: Node22.13.0 16 pass / 0 fail / 0 skip / 0 TODO, independent exit 0; Bun1.3.14 16 pass / 0 fail, independent exit 0. Raw logs are `/private/tmp/vibe-fs-mf01-callable-node-first.log` and `/private/tmp/vibe-fs-mf01-callable-bun-first.log`. Cases cover separate proof/execution and receiver/closure with non-JSON sanity arguments; invalid metadata and JsFailure families; object-veto proof and runaway abort getter; forged/stale/cross-proof/cross-run refs; lost method reply with one physical mutation; lost finish confirmation and a new offer; caught abandon with actual write/rm and refused park; looping finally killed without program-settled claim; externally terminated constructor, getter, sanity thunk, after-await callback, thenable and toJSON loops.

The copied original A ten cases also passed against this B mechanism, sequentially Node22 10/0/0skip/0TODO, exit 0; Bun 10/0, exit 0. Logs `/private/tmp/vibe-fs-mf01-callable-gates-node-first.log` and `/private/tmp/vibe-fs-mf01-callable-gates-bun-first.log`. Their source remains byte-identical to original A, so they do not incorporate A's two newer terminal tests; B has the same single-admission terminal helper and its own abandon cases, and independent cancellation review is still required.

Every returned owner asserts actual native Closed, null physical failure and PID ESRCH. Runtime logs print each owned PID/native exit and transport faults. Normal completions and completed finally exited 0. Runaway/lost-method-reply/looping-finally controls closed by actual SIGKILL; their assertions require this precise result. Startup and retained-close observation injection keep their original distinction from real OS failures. No current owned test process remains.

Frozen nine-file manifest `/private/tmp/vibe-fs-mf01-callable-pre-run.sha256` is unchanged after all four runs (`...-inputs-after.log`, `...-freeze-check.log`). Key hashes: owner `aafe9127b23eac82cbaf57865dd0caeeafb3f99abbb3c69fee9b2f386dad3989`, guest `026bf0135f2270ef745513583803a4e3a42ef5268bb61b860a703a745c438491`, trusted realm `6479295d54a620ce4e1c56b0c26b4ca27df1815bf6922639485658aec70f5f15`, Callable tests `50111bbab9b6c0901536c42c1db1913448ccc59774e75c6c391a124fe9f1fb6b`. The remaining five hashes match the copied A inputs. Candidate source is now frozen for independent review; none of these local proofs is production reception or a complete sandbox contract.

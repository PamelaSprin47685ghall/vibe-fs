# vs-006 — `requirements/verification-system` 006 remains RED: precise root cause

Scope of this note: the **diagnostic** account of why `requirements/verification-system/tests/006.test.mjs`
is red on a loaded box. It records what was measured and where the evidence lives. It does **not** change any
assertion, budget, or product watchdog, and it does **not** claim the suite is fixed — it is still RED.

## 1. Verdict up front

- `requirements/verification-system/tests/006.test.mjs` is **still RED**. This note changes nothing about that.
- **No genuine verdict is lost.** The failures are budget exhaustion *before the first verdict can exist*, plus a
  teardown-side `ps` timeout. No test's assertion outcome was mis-reported.
- The red is **workload dependent**: it reproduces under severe machine contention (loadavg 21.6/32.4/36.4 on
  4 CPUs; bare `node -e 0` costs 361–748 ms). On an idle box the same fixtures pass.
- Nothing in the repo was changed to obtain this diagnosis: no assertion, no `silenceMs`/`SUITE_BACKSTOP_MS`
  budget, no product watchdog.

## 2. Mechanism: the silence window is armed before the work exists

The supervisor arms its verdict-silence window **before** it spawns the inner runner:

| step | location | what happens |
|---|---|---|
| watchdog armed | `requirements/verification-system/tests/e2e/support/supervise-node-test.mjs:336-337` | `new Watchdog({ timeoutMs: silenceMs, … })` |
| inner runner spawned **later** | `…/supervise-node-test.mjs:368` | `spawn(process.execPath, [inner, …])` |
| progress baseline | `…/e2e/support/watchdog.js:27` | baseline is `"start lane=startup"`, `expectationId: null` |
| only a verdict IPC renews the window | `…/supervise-node-test.mjs:452-453` | `watchdog.advance(progress)` only on a classified verdict event |
| first progress event is late | `…/tests/support/run-inner.mjs:160` | `runner:file-start` is sent only **after** `run()` has been called |

So during the whole startup the file is still *queued* — it has produced no progress the watchdog can count —
while the wall clock is already burning.

## 3. Measurement — exact z1 sample (diagnostic instrumented run, **relaxed** window)

`/home/hermes/tmp/vs-006/repro/runs/z1/timing.log` is an instrumented run with `silenceMs: 30000`
(`runs/z1/supervise.mjs:2`). It is a **diagnostic instrumented relaxed-window run — NOT the unchanged
acceptance configuration**. Its value is that it timestamps each stage on the wall clock:

| marker | wall clock | delta |
|---|---|---|
| `spawn-inner` | `1792631019784` | baseline (spawn of the inner runner) |
| `runner:file-start` IPC (first progress event) | `1792631021234` | **+1450 ms after spawn** |
| `body-enter` (first test body entered) | `1792631022582` | **+2798 ms after spawn** |
| first `test:complete` (inner) | `1792631022993` | **+3209 ms after spawn** |

The unchanged budget is 1000 ms for the fixture-level windows (`silenceMs: 1000` at
`…/tests/support/supervised-tool-reclamation-tests.mjs:248` and `…/tests/006.test.mjs:868`) and 5000 ms for the
suite-level unit window (`UNIT_VERDICT_SILENCE_MS`,
`…/tests/e2e/support/time-budget.js:149`). The measured startup cost therefore already exceeds the 1000 ms
fixture windows **before any test body runs**, and it consumes most of the 5000 ms suite window.

## 4. Measurement — unchanged-window sample (s5)

`/home/hermes/tmp/vs-006/repro/runs/s5/out.txt` is the same fixture at the **unchanged** `silenceMs: 1000`
(`runs/s5/supervise.mjs:2`):

```
CODE 1
runner: 1 test file(s), 1000ms verdict-silence window
WATCHDOG: 'synchronous-verdicts' silent for 1030ms (limit 1000ms); 0 blocking progress update(s), last progress: start lane=startup expectation=none
runner: 1 file(s) had not reported completion
runner: file streams: 0 drained, 0 active, 1 queued
runner: 1 queued file(s) have not started
runner: verdict counts unavailable; no authoritative summary before the silence
```

`0 blocking progress update(s)` and `1 queued` — the file had not even started. Taken together with the z1
sample, the two runs show **startup budget exhaustion** at both windows, not a lost verdict.

## 5. Failure classification

From the failing-set capture in
`/home/hermes/tmp/vs-006/test006-with-redunit-dist.out` (59 cases, **46 passed / 13 failed**, exit 1; the
failure count and membership jitter between runs because the race is timing-dependent — an earlier
red-unit capture saw 15 and 14 failures over two runs):

| class | count | detail |
|---|---|---|
| ownership-marker / diagnostic-marker | 7 | detached-tool reclamation plus the 6 native inspection-failure scenarios. The supervisor's first `inspectProcessTree` runs at ~+1105 ms while `tool-ownership.json` is only written at ~+5416 ms (after the monitor/tool/child exist and after real `ps` calls) ⇒ `ENOENT` at `…/tests/support/supervised-tool-reclamation-tests.mjs:152`, again in the caller's catch at `:254`; `caught.json` (`:257`) is never written, so the assertion at `:307` ("The caller did not record its caught failure") fires. The marker write is a **consequence** of the leaf never getting far enough. |
| `caught.json` ENOENT (normal-exit residual) | 1 | `…/tests/support/normal-exit-residual-support.mjs:164` reads `owned.json`, which is never written because the leaf was killed at the 5000 ms silence. |
| pure silence-window expiry before any file-start | 5 | the 300 ms worker-cost / body-start windows (×2), the 1000 ms file-wait diagnostics window, the 1000 ms `silence-caller-cleanup` case (`…/tests/006.test.mjs:868`, assertion at `:893-894` — the held-leaf marker never appears), and the 3 `synchronous-verdicts` cases. |
| physical backstop teardown | 1 | `SUITE_BACKSTOP_MS` (1500 ms in that capture) fires first, and the teardown `ps` then hits its 2000 ms cap (`PROCESS_TREE_TIMEOUT_MS`, `…/tests/e2e/support/time-budget.js:229`) ⇒ `spawnSync ps ETIMEDOUT` via `…/tests/e2e/support/supervise-node-test.mjs:76` (`:82`, `:134`). |

**Not all of the failures are startup.** The ownership/diagnostic-marker class and the `caught.json` class are
*downstream* of the same budget exhaustion (the leaf never reaches the point where it writes its marker), and the
physical-backstop class additionally fails in **teardown**, where `ps` itself times out against the 2000 ms
`PROCESS_TREE_TIMEOUT_MS` cap. That teardown path is a separate symptom from the startup race and must not be
folded into "it is only startup".

## 6. Control runs — diagnostic only, **not** pass acceptance

- Same fixture at `silenceMs: 30000` (relaxed, diagnostic): **exit 0**, 8/8 passed, full `runner:summary`
  (`/home/hermes/tmp/vs-006/repro/runs/z1/out.txt`: `[test-summary] 1 file(s), 8 passed, 0 failed`,
  `8 passed, 0 failed; 1/1 planned file(s) completed`).
- `run-inner.mjs` alone (no supervisor): `8 passed, 0 failed`.

These controls establish that the fixture logic and the verdict transport are sound and that the failure is
**budget/ordering**, not a lost verdict. **A 30000 ms window is a diagnostic instrument only and is explicitly
not an acceptance configuration** — no suite may be declared passed on the strength of a relaxed-window run.
The unchanged acceptance configuration remains `silenceMs: 1000` (fixture windows) / `UNIT_VERDICT_SILENCE_MS`
(suite window), and under it the suite is still RED.

## 7. Where the local evidence lives

| artifact | path |
|---|---|
| exact z1 instrumented timing log | `/home/hermes/tmp/vs-006/repro/runs/z1/timing.log` |
| z1 relaxed-window control output | `/home/hermes/tmp/vs-006/repro/runs/z1/out.txt` |
| s5 unchanged-window output (`0 progress / 1 queued`) | `/home/hermes/tmp/vs-006/repro/runs/s5/out.txt` |
| full 006 failing-set capture (46/59 pass) | `/home/hermes/tmp/vs-006/test006-with-redunit-dist.out` |
| 006 run without a usable `dist/` (module-not-found) | `/home/hermes/tmp/vs-006/test006.out` |
| repro harness / fixtures | `/home/hermes/tmp/vs-006/repro/` |

These are **local scratch artifacts** (outside the repository) and are not committed; they are referenced here so
the measurement can be re-derived rather than taken on trust.

## 8. What this note does *not* do

- It does not change any assertion, budget, or product watchdog.
- It does not mark the suite green, and it does not propose a relaxation as an acceptance rule.
- It does not attribute the failure to any product defect: the evidence points at the supervisor's window
  ordering under load plus a `ps` timeout in teardown.
- The red remains open. Fixing it is a separate decision (window ordering / causal progress channel / teardown
  `ps` budget) and is not taken here.

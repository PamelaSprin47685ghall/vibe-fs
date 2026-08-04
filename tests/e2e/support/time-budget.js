/**
 * time-budget.js — every wall-clock bound the harness owns, named exactly once.
 *
 * VERIFY-004: 「wall-clock 上限可以作为兜底存在，但不得是唯一或首要的判据。兜底值必须集中定义，
 * 不得散落为字面量。」 Package W1 measured what 散落 had become: 23 timing literals across 13
 * files, none of them visible to any gate. Two were the same 3000ms diagnostic race written
 * independently in two files. One (10000) was spelled a third and fourth time inside
 * user-facing strings that would have kept saying "within 10s" after the budget moved. One
 * (2000) had three declarations under two spellings, plus a fourth site that named it in a
 * default parameter without importing it — so that default would have thrown a ReferenceError
 * if any caller had ever omitted the argument.
 *
 * The magnitude threshold IS the semantic line, which is what makes this file mechanically
 * enforceable. A polling slice must poll faster than the budget that bounds it, so a
 * legitimate slice is below 1000ms by construction — the fact loop in `canary-driver.mjs`
 * slices at 500ms under the 3000ms silence budget, the listen poll runs at 50ms, the socket
 * retry at 30ms. Anything at or above 1000ms is therefore a budget rather than a slice, and a
 * budget belongs here, where raising it is one visible diff instead of a quiet edit at a call
 * site. `scripts/budget-gate.mjs` enforces exactly that, and deliberately offers no exemption
 * channel: every pseudo-gate package W is replacing rotted through one.
 *
 * Values are moved verbatim; this is a migration, not a retuning. Where two call sites held
 * the same number for the same reason they now share one name; where they held the same number
 * for different reasons they keep separate names, because collapsing those would decide
 * something a migration is not entitled to decide.
 */

// ── the gate's own discriminator ─────────────────────────────────────────────

/**
 * At or above this, a millisecond literal is a budget rather than a polling slice, and
 * `scripts/budget-gate.mjs` refuses it outside this file. The threshold lives here for the same
 * reason everything else does: it is the one number that decides what every other number means,
 * so it is the last one that should be a literal in a script.
 *
 * Not arbitrary. A slice must poll faster than the budget bounding it, and the tightest budget
 * here is WATCHDOG_TIMEOUT_MS at 3000, against which the real slices measure 500, 100, 50, 30.
 */
export const LITERAL_BUDGET_THRESHOLD_MS = 1000;

/**
 * 依赖注入入口：同名环境变量覆盖默认值，让门禁用例把同一套监督语义跑到更小的时间尺度。
 *
 * 判据不依赖值的绝对大小，依赖值之间的不等式（静默窗口 > 单测界、轮询切片 < 窗口、
 * 合法工作总量 > 窗口），故整体缩放不破坏任何判据，只缩短墙钟。只接受正有限数；非法值
 * 拒绝启动而非静默回退——一个拼错的环境变量若回退默认值，门禁会在错误的窗口上给出绿灯。
 */
const budgetFromEnv = (name, fallback) => {
  const raw = process.env[name];
  if (raw === undefined) return fallback;
  const value = Number(raw);
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`${name} must be a positive finite number, got ${JSON.stringify(raw)}`);
  }
  return value;
};

// ── causal progress ─────────────────────────────────────────────────────────

/**
 * Silence budget of a scenario-local watchdog: how long without causal progress before the
 * canary is declared hung. Short on purpose — VERIFY-004 asks 「距上次因果进展过了多久」, and 3s
 * means the diagnostic is taken at the causal scene instead of minutes downstream. Also the
 * default budget for a canary `wait` expectation (`strict-mock-provider.js:154`), so a single
 * fork→worktree bootstrap or join mailbox gate (~2.3-2.4s) fits inside it without a per-step
 * override.
 */
export const WATCHDOG_TIMEOUT_MS = budgetFromEnv('WATCHDOG_TIMEOUT_MS', 3000);

/**
 * Ceiling on the watchdog's own teardown once it has already decided to fire. `watchdog.js`
 * and `scenario-parallel.js` each held this as an independent 3000 literal for one idea: the
 * diagnostic dump and `host.stop` must not themselves hang, so they race a timer. Larger than
 * the silence budget is correct here — this runs after the verdict, so it costs a healthy run
 * nothing, and truncating the dump would breach 「删除 watchdog 的诊断转储」.
 */
export const DIAGNOSTIC_RACE_MS = 3000;

/**
 * Window for the 就绪门禁: a canary that has not printed its exact ready marker within this is
 * failed, not waved through. Previously three literals — the timer plus two failure strings
 * spelling the same value as "10s" — which is how a message outlives the budget it describes.
 */
export const CANARY_READY_MS = 10000;

/**
 * How long one startup STAGE may be silent before the canary is declared stuck (W5).
 *
 * Replaces `CANARY_READY_MS` as the startup criterion; that constant survives as the total 兜底.
 * The clause forbids a window with no causal criterion inside it, and a flat ten seconds from spawn
 * to ready was exactly that: a canary that bound its port and then wedged was indistinguishable
 * from one still compiling.
 *
 * Per stage rather than per startup, so total startup time is unbounded in the same way a healthy
 * canary's runtime is — what is bounded is silence. Four seconds because the slowest observed stage
 * is `host.start`'s health wait, and it must survive fifteen canaries competing for one machine.
 */
export const READINESS_STAGE_MS = 4000;

/**
 * Fallback ceiling for one canary process: the 兜底 VERIFY-004 permits so long as it is not the
 * primary criterion, which the watchdog is. Dual-script restart canaries need roughly 45s solo,
 * doubled for parallel host load. Still overridable through the CANARY_TIMEOUT_MS environment
 * variable; the env read stays at the call site so this module reads no process state.
 */
export const CANARY_TIMEOUT_MS = 90000;

/**
 * Outer wall-clock window of a `waitFact` barrier, whose real criterion is journal facts
 * appearing. A publish/fast-forward chain crosses many host events, so the span is long;
 * whether that loop's renewal is genuinely causal is package W6's subject, and this constant
 * is only the fallback it must not be leaning on.
 */
export const WAIT_FACT_WINDOW_MS = 120000;

/**
 * How long the nudge canary reconciles the fork tool from the session API before giving up.
 * Long because it covers a real fork plus a nudge round trip; the loop inside it feeds the
 * watchdog whenever a fork completes, so this bound is reached only when nothing completes.
 */
export const FORK_COMPLETION_WINDOW_MS = 10000;

/**
 * One slice of that reconcile loop: how long it waits for the next session event before
 * re-reading the API. Note for W6 — this sits below the silence budget (2000 < 3000), and the
 * loop feeds the watchdog only when a fork completes, so a slice that runs to its full length
 * cannot outlast the watchdog's window; it would have at the old 2000/2000 equality.
 * Centralizing it is what makes that visible; changing it is a semantic decision W6 owns, not a
 * migration.
 */
export const FORK_RECONCILE_SLICE_MS = 2000;

/**
 * ENFORCER-160 polling slice for the parked-transform canary. Must be faster
 * than the window it sits inside (WATCHDOG_TIMEOUT_MS) — a slice that is not
 * strictly smaller than its bound is the bound, not a probe.
 */
export const ENFORCER_POLL_SLICE_MS = 500;

// ── unit suite (layers 1-3) ─────────────────────────────────────────────────

/**
 * Hard per-test bound for `tests/unit`. A pure fold or a fake-clock trajectory has no reason to
 * take a second, so this doubles as a design constraint: raising it is how a race gets papered
 * over (VERIFY-002).
 */
export const PER_TEST_TIMEOUT_MS = budgetFromEnv('PER_TEST_TIMEOUT_MS', 1000);

/**
 * Whole-suite ceiling, now a 兜底 rather than the hang criterion.
 *
 * Before W4 this WAS the real criterion: a test that hangs while holding a handle prevents
 * node:test from emitting `end`, so nothing else terminated the run. VERIFY-004 forbids that
 * (「以套件总时长作为唯一挂死判据」) and W4 removed it — `UNIT_VERDICT_SILENCE_MS` below is the
 * primary criterion, and this survives only for a child that outlives its supervisor.
 *
 * The clause permits exactly this: 「wall-clock 上限可以作为兜底存在，但不得是唯一或首要的判据」.
 */
export const SUITE_BACKSTOP_MS = budgetFromEnv('SUITE_BACKSTOP_MS', 300000);

/**
 * How long the unit suite may go without a test VERDICT before it is declared hung.
 *
 * The primary hang criterion for `tests/unit`, fed by `test:pass` / `test:fail` / `test:complete`.
 * Not `PER_TEST_TIMEOUT_MS`, because a verdict arrives only after a test finishes: node:test's
 * timeout is a verdict line rather than an abort line (measured), so an overrunning test keeps
 * running and the gap between two verdicts can legitimately exceed the per-test bound. Three times
 * that bound covers the overrun plus scheduling jitter under `concurrency: true` while still ending
 * a genuinely hung run two orders of magnitude sooner than the backstop.
 */
export const UNIT_VERDICT_SILENCE_MS = budgetFromEnv('UNIT_VERDICT_SILENCE_MS', 3000);

// ── waiting for one semantic event ──────────────────────────────────────────

/**
 * Default window for a single awaited semantic event — one SSE event, one prompt reaching idle,
 * one scenario step. Four files defaulted to this same 1000 for this same reason.
 */
export const DEFAULT_AWAIT_TIMEOUT_MS = 1000;

/**
 * How long a "this must never arrive" assertion waits before absence counts as proven.
 * Deliberately longer than DEFAULT_AWAIT_TIMEOUT_MS: a negative claim is only as strong as the
 * window it watched.
 */
export const DEFAULT_NEVER_TIMEOUT_MS = 5000;

/**
 * Window for one awaited event inside the harness's own gate cases, which drive a local fake
 * SSE server rather than a Host. Wider than DEFAULT_AWAIT_TIMEOUT_MS because these cases run
 * under `test:harness` alongside process spawns competing for the same CPU.
 */
export const GATE_PROBE_TIMEOUT_MS = 3000;

/**
 * Silence window for real `opencode serve` to print its listening marker inside a gate case.
 * Missing this bound means the startup ladder lacks progress or does too much work; scheduler
 * contention is not grounds for widening the criterion.
 */
export const GATE_HOST_START_TIMEOUT_MS = 1000;

/**
 * How long teardown waits for each session to report idle after an abort. Best-effort — the
 * caller swallows the rejection, because a session that never idles is caught by the leak check
 * that follows, with a better diagnostic than a bare timeout.
 */
export const TEARDOWN_IDLE_MS = 2000;

// ── process lifecycle ───────────────────────────────────────────────────────

/** Grace after SIGTERM before escalating, so a Host gets to run its own shutdown. */
export const SIGTERM_GRACE_MS = 5000;

/** Grace after SIGKILL before the process is reported un-killable. Was declared twice. */
export const SIGKILL_GRACE_MS = 1000;

/** Ceiling on the `ps`/`pgrep` call that enumerates a process tree during leak checks. */
export const PROCESS_TREE_TIMEOUT_MS = 2000;

/** How long a port may still accept connections after dispose before it counts as leaked. */
export const SOCKET_CHECK_TIMEOUT_MS = 2000;

/** Window for `opencode serve` to print its listen line and then answer a health probe. */
export const HOST_START_TIMEOUT_MS = 5000;

/**
 * Minimum age before a process carrying no run-id marker may be reaped as an orphan. Ownership
 * is the primary criterion, so this is only the 兜底 for a bare process that never inherited the
 * marker — hence short. Overridable through REAPER_ORPHAN_MIN_AGE_MS.
 */
export const ORPHAN_MIN_AGE_MS = 5000;

/**
 * How long a spawn-ledger entry stays authoritative. Far longer than anything else here because
 * it bounds a different thing: not how long to wait for progress, but how long a record of a
 * spawned process remains worth acting on. Was written as `30 * 60 * 1000`, which the gate reads
 * as three separate numbers and a reader as none.
 */
export const LEDGER_ENTRY_TTL_MS = 1800000;

// ── suite-level loops ───────────────────────────────────────────────────────

/**
 * Outer window for a whole canary scenario suite, chosen to fire before the 600s CI ceiling so
 * the failure is reported by the harness with diagnostics rather than by a killed process.
 */
export const SCENARIO_SUITE_WINDOW_MS = 500000;

/** Per-run ceiling in the stability gate, which repeats one scenario up to three times. */
export const STABILITY_SCENARIO_TIMEOUT_MS = 30000;

/** Outer window for all runs of the stability gate together. */
export const STABILITY_GATE_WINDOW_MS = 300000;

/**
 * Least remaining time worth starting another stability run with. Below this the gate stops
 * early and says so, rather than starting a run it knows the outer window will cut short — a
 * truncated run would be reported as a failure it did not have.
 */
export const STABILITY_MIN_RUN_MS = 5000;

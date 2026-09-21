/**
 * watchdog.js — the silence criterion of verification-system-006, derived from the clause.
 *
 * 「没有进展就杀死，而不是等总超时」. The judgement is 「距上次因果进展的静默时长」, never the total
 * run time, and the four properties the clause spells out are:
 *
 *   only semantic events feed it        a consumed script step, an explicit checkpoint (turn
 *                                      activity, assistant terminal, idle-after-activity), a
 *                                      Host restart stage. NOT raw SSE, NOT provider HTTP,
 *                                      NOT session.created-class lifecycle noise, NOT any
 *                                      evidence that 「有字节在动」
 *   background progress is recorded,    advance(blocking = true)  resets the silence timer
 *   never renewed                       advance(blocking = false) records only
 *   the dump answers 「最后一次进展是什么」  reason, lane, and how long ago the last background
 *                                      progress was; exit code alone is the degradation
 *   the timer holds no handle           a scenario that closed everything exits at once, so
 *                                      the watchdog fires only while something (a hung SSE
 *                                      reader, a leaked server) is still keeping the loop up
 *
 * ── what the previous implementation already had ────────────────────────────
 *
 * Three of the four. It defaulted `blocking` to true and recorded `blocking: false` without
 * re-arming, it unref'd its timer, and it dumped the last reason, lane, and expectation plus
 * the background age before exiting non-zero. Package W rebuilds it because the charter
 * mandates 第一性原理瀑布流 for this package, not because those three were wrong — a reader who
 * takes this rewrite as evidence the old class was broken would draw the wrong lesson.
 *
 * What measurably WAS wrong is one line of the dump: `this._count` incremented on background
 * advances too, so a scenario whose only activity was a blogger sidecar printed
 * `7 progress update(s)` beside `last: start lane=startup`. Both halves true, and together they
 * report the opposite of what happened — the reader concludes seven causal steps ran and stalled
 * at startup, when zero ran. So the counters are separate here, and the blocking one is labelled
 * as such: 「诊断必须包含『最后一次进展是什么』，否则 watchdog 只是一个更快的超时」 is not
 * satisfied by a number that counts the wrong thing.
 *
 * ── the feed is validated, because mjs cannot rename-check it ───────────────
 *
 * `advance` rejects a call without a `reason` or a `lane`. This repo has measured the
 * alternative four times (`parentSession`, `faultFor`, `boundaryFor`, the empty
 * `resetHeartbeat`): a field that silently reads `undefined` produces a mechanism that runs,
 * stays green, and describes nothing. A default of `'unattributed'` would keep every canary
 * passing while the dump lost the one thing the clause requires it to carry, so a renamed field
 * has to be loud. Every live call site already passes both, so this validation is a lock on
 * what exists, not a new demand: `scenario-parallel.js` (restart stages and consumed
 * expectations), `scenario-turn.js` (turn checkpoints), `canary-driver.mjs` (flow verbs),
 * `companion-canary.mjs` and `host-nudge-canary.mjs` (scenario-specific checkpoints).
 *
 * `blocking` must be a real boolean when present. `blocking: 'false'` under a `!== false` test
 * renews, which is the background-lane degradation reached by a type rather than by an intent.
 *
 * ── coming consumer ─────────────────────────────────────────────────────────
 *
 * Package W4 will drive this class from `tests/unit/runner.mjs` as an out-of-process supervisor
 * over the unit suite, fed by node:test verdicts (`test:pass` / `test:fail` blocking,
 * `test:stdout` / `test:diagnostic` background). The constructor shape and `advance` / `stop`
 * are therefore held stable, and `process.exit` staying inside `_fire` is deliberate: the
 * supervisor's whole value is that it can end a child that a hung test is holding.
 */

import { DIAGNOSTIC_RACE_MS, WATCHDOG_TIMEOUT_MS } from './time-budget.js';

export class Watchdog {
  /**
   * @param {{
   *   timeoutMs?: number,
   *   label?: string,
   *   onTimeout?: () => Promise<void> | void,
   *   deps?: {
   *     clock?: { nowMs: () => number },
   *     timers?: { schedule: (ms: number, cb: () => void) => any, cancel: (handle: any) => void },
   *     diagnostic?: { write: (msg: string) => void },
   *     terminate?: () => void,
   *   }
   * }} opts
   *
   * `timeoutMs` defaults to the centralized silence window; 「静默窗口集中定义为唯一常量」. A
   * caller may narrow it (gate cases run at 150ms so a case costs milliseconds), but not omit
   * it into a literal — there is no fallback number in this file.
   *
   * Optional `deps` enables deterministic virtual-time testing while preserving all default
   * production semantics when omitted:
   * - `clock.nowMs()`: defaults to `Date.now`
   * - `timers.schedule(ms, cb)` / `timers.cancel(handle)`: defaults to `setTimeout(cb, ms)` (with `unref`) / `clearTimeout(handle)`
   * - `diagnostic.write(msg)`: defaults to `console.error(msg)`
   * - `terminate()`: defaults to `process.exit(1)`
   */
  constructor({ timeoutMs = WATCHDOG_TIMEOUT_MS, label, onTimeout, deps } = {}) {
    this._clock = {
      nowMs: deps?.clock?.nowMs || (() => Date.now()),
    };
    this._timers = {
      schedule: deps?.timers?.schedule || ((ms, cb) => {
        const t = setTimeout(cb, ms);
        t.unref?.();
        return t;
      }),
      cancel: deps?.timers?.cancel || ((h) => clearTimeout(h)),
    };
    this._diagnostic = {
      write: deps?.diagnostic?.write || ((msg) => console.error(msg)),
    };
    this._terminate = deps?.terminate || (() => process.exit(1));

    if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
      throw new Error(`Watchdog requires a positive silence window, got ${timeoutMs}`);
    }
    this._timeoutMs = timeoutMs;
    this._label = label || 'canary';
    this._onTimeout = onTimeout || null;
    this._blockingCount = 0;
    this._backgroundCount = 0;
    // The clock starts at construction, so the window before the first step is covered too
    // (「覆盖必须无缝」). 'start' / 'startup' are what the dump then reports, and a dump saying
    // the last progress was the start is a true statement about a scenario that never moved.
    this._lastProgressAt = this._clock.nowMs();
    this._lastProgress = { reason: 'start', lane: 'startup', expectationId: null };
    this._lastBackground = null;
    this._stopped = false;
    this._timer = null;
    this._arm();
  }

  /**
   * Feed one semantic event.
   *
   * The caller decides whether an event proves the causal chain moved — that judgement needs
   * the scenario's context and cannot be made here. What this enforces is that the claim is
   * attributable: a reason, a lane, and an explicit lane class.
   */
  advance(progress) {
    if (this._stopped) return;
    const update = progress || {};
    const reason = requireText(update.reason, 'reason');
    const lane = requireText(update.lane, 'lane');
    const expectationId = update.expectationId ?? null;
    const blocking = resolveBlocking(update.blocking);

    if (!blocking) {
      this._backgroundCount += 1;
      this._lastBackground = { at: this._clock.nowMs(), reason, lane, expectationId };
      return;
    }

    this._blockingCount += 1;
    this._lastProgressAt = this._clock.nowMs();
    this._lastProgress = { reason, lane, expectationId };
    this._arm();
  }

  /** Disarm for good: the scenario reached its verdict, so silence no longer means anything. */
  stop() {
    this._stopped = true;
    this._timers.cancel(this._timer);
    this._timer = null;
  }

  /**
   * Temporarily widen the silence window for one explicitly-bounded wait step
   * (verification-system-006: a legitimately slow step must be declared, never inferred —
   * the scenario's `timeoutMs` is that declaration). `null` restores the
   * centralized default. Only blocking advances renew the widened window, so
   * background traffic still cannot extend it.
   */
  setWindow(windowMs) {
    if (this._stopped) return;
    this._timeoutMs = windowMs ?? WATCHDOG_TIMEOUT_MS;
    this._arm();
  }

  _arm() {
    this._timers.cancel(this._timer);
    this._timer = this._timers.schedule(this._timeoutMs, () => {
      this._fire().catch(() => this._terminate());
    }, this._timeoutMs);
  }

  async _fire() {
    if (this._stopped) return;
    this._stopped = true;

    this._diagnostic.write(
      `WATCHDOG: '${this._label}' silent for ${this._clock.nowMs() - this._lastProgressAt}ms ` +
      `(limit ${this._timeoutMs}ms); ${this._blockingCount} blocking progress update(s), ` +
      `last progress: ${this._lastProgress.reason} lane=${this._lastProgress.lane} ` +
      `expectation=${this._lastProgress.expectationId || 'none'}`,
    );
    if (this._lastBackground) {
      // Printed only when a background lane actually ran. An age line for a lane that never
      // reported would read as a stalled sidecar and send the reader after a lane that does
      // not exist.
      this._diagnostic.write(
        `WATCHDOG: background progress ${this._clock.nowMs() - this._lastBackground.at}ms ago: ` +
        `${this._lastBackground.reason} lane=${this._lastBackground.lane} ` +
        `(${this._backgroundCount} background update(s), none of them renewals)`,
      );
    }

    // The scenario's own dump (event tail, pending script steps) races a ceiling: it talks to a
    // Host that may be the thing that hung, and a dump that hangs turns the watchdog back into
    // the total timeout it exists to replace. The race bounds the dump, it does not truncate it.
    try {
      if (this._onTimeout) {
        await Promise.race([
          this._onTimeout(),
          new Promise((resolve) => this._timers.schedule(DIAGNOSTIC_RACE_MS, resolve)),
        ]);
      }
    } catch {}

    // Flush diagnostic lines before hard-exit; otherwise a piped parent (unit-runner
    // probes on CI) can observe exit without ever reading WATCHDOG stderr.
    try {
      process.stderr.write('');
    } catch {}
    this._terminate();
  }
}

function requireText(value, field) {
  if (typeof value !== 'string' || value.length === 0) {
    throw new TypeError(
      `Watchdog.advance requires a non-empty ${field}; verification-system-006 makes it part of the ` +
      `timeout dump, and ${JSON.stringify(value)} would leave the dump unable to say what the ` +
      `last progress was`,
    );
  }
  return value;
}

function resolveBlocking(value) {
  if (value === undefined) return true;
  if (typeof value !== 'boolean') {
    throw new TypeError(
      `Watchdog.advance blocking must be a boolean, got ${JSON.stringify(value)}; a truthy ` +
      `non-boolean would renew the silence timer for a background lane`,
    );
  }
  return value;
}

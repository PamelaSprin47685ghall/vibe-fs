/**
 * Track silence since the monitored target last advanced (verification-system-006).
 * Callers supply attributable progress; background observations never renew the timer.
 * Virtual clock/timer ports exercise the same transitions as physical supervision.
 */
import { DIAGNOSTIC_RACE_MS, WATCHDOG_TIMEOUT_MS } from './time-budget.js';

export class Watchdog {
  constructor({ timeoutMs = WATCHDOG_TIMEOUT_MS, label, onTimeout, deps } = {}) {
    this._clock = { nowMs: deps?.clock?.nowMs || (() => Date.now()) };
    this._timers = {
      schedule: deps?.timers?.schedule || ((ms, cb) => {
        const timer = setTimeout(cb, ms);
        timer.unref?.();
        return timer;
      }),
      cancel: deps?.timers?.cancel || ((handle) => clearTimeout(handle)),
    };
    this._diagnostic = { write: deps?.diagnostic?.write || ((msg) => console.error(msg)) };
    this._terminate = deps?.terminate || (() => process.exit(1));
    requireWindow(timeoutMs);
    this._timeoutMs = timeoutMs;
    this._label = label || 'canary';
    this._onTimeout = onTimeout || null;
    this._blockingCount = 0;
    this._backgroundCount = 0;
    this._lastProgressAt = this._clock.nowMs();
    this._lastProgress = { reason: 'start', lane: 'startup', expectationId: null };
    this._lastBackground = null;
    this._stopped = false;
    this._timer = null;
    this._arm();
  }

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

  stop() {
    this._stopped = true;
    this._timers.cancel(this._timer);
    this._timer = null;
  }

  // Changing the limit is not evidence of new progress. Null restores the shared default.
  setWindow(windowMs) {
    if (this._stopped) return;
    const timeoutMs = windowMs ?? WATCHDOG_TIMEOUT_MS;
    requireWindow(timeoutMs);
    this._timeoutMs = timeoutMs;
    this._arm();
  }

  _arm() {
    this._timers.cancel(this._timer);
    const remainingMs = Math.max(0, this._lastProgressAt + this._timeoutMs - this._clock.nowMs());
    this._timer = this._timers.schedule(remainingMs, () => {
      this._fire().catch(() => this._terminate());
    });
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
      this._diagnostic.write(
        `WATCHDOG: background progress ${this._clock.nowMs() - this._lastBackground.at}ms ago: ` +
        `${this._lastBackground.reason} lane=${this._lastBackground.lane} ` +
        `(${this._backgroundCount} background update(s), none of them renewals)`,
      );
    }

    // A diagnostic collector may depend on the same stalled Host it is inspecting.
    let diagnosticTimer;
    try {
      if (this._onTimeout) {
        await Promise.race([
          this._onTimeout(),
          new Promise((_, reject) => {
            diagnosticTimer = this._timers.schedule(DIAGNOSTIC_RACE_MS, () => {
              reject(new Error('diagnostic deadline exceeded'));
            });
          }),
        ]);
      } else {
        this._diagnostic.write('WATCHDOG: current waits unavailable: no diagnostic collector configured');
      }
    } catch (error) {
      this._diagnostic.write(`WATCHDOG: current waits unavailable: ${error?.message || String(error)}`);
    } finally {
      if (diagnosticTimer !== undefined) this._timers.cancel(diagnosticTimer);
    }
    this._terminate();
  }
}

function requireWindow(value) {
  if (!Number.isFinite(value) || value <= 0) {
    throw new Error(`Watchdog requires a positive silence window, got ${value}`);
  }
}

function requireText(value, field) {
  if (typeof value !== 'string' || value.length === 0) {
    throw new TypeError(`Watchdog.advance requires a non-empty ${field}; got ${JSON.stringify(value)}`);
  }
  return value;
}

function resolveBlocking(value) {
  if (value === undefined) return true;
  if (typeof value !== 'boolean') {
    throw new TypeError(`Watchdog.advance blocking must be a boolean, got ${JSON.stringify(value)}`);
  }
  return value;
}

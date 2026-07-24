/**
 * watchdog.js — Silence watchdog ported from the tests-next runner.
 *
 * tests-next: every assertion IPCs a heartbeat; 1s of silence SIGKILLs
 * the worker process group. testkit canaries run in-process, so the
 * heartbeats are observable progress (SSE events, provider requests,
 * HTTP client calls, explicit pet() checkpoints) and the kill is
 * process.exit(1) after a best-effort diagnostic dump. A runaway
 * canary costs one silence window instead of hanging until the outer
 * CI timeout.
 *
 * The timer is unref'd: once every other handle closes the process
 * exits naturally — the watchdog only fires while something (a hung
 * SSE reader, a leaked server) still keeps the event loop alive.
 */
export class Watchdog {
  constructor({ timeoutMs, label, onTimeout }) {
    if (!Number.isFinite(timeoutMs) || timeoutMs <= 0) {
      throw new Error('Watchdog requires timeoutMs > 0');
    }
    this._timeoutMs = timeoutMs;
    this._label = label || 'canary';
    this._onTimeout = onTimeout || null;
    this._count = 0;
    this._lastAt = Date.now();
    this._lastReason = 'start';
    this._stopped = false;
    this._arm();
  }

  pet(reason) {
    if (this._stopped) return;
    this._count++;
    this._lastAt = Date.now();
    this._lastReason = reason || 'progress';
    this._arm();
  }

  stop() {
    this._stopped = true;
    clearTimeout(this._timer);
  }

  _arm() {
    clearTimeout(this._timer);
    this._timer = setTimeout(() => {
      this._fire().catch(() => process.exit(1));
    }, this._timeoutMs);
    this._timer.unref?.();
  }

  async _fire() {
    if (this._stopped) return;
    this._stopped = true;
    console.error(
      `WATCHDOG: '${this._label}' silent for ${Date.now() - this._lastAt}ms ` +
      `(limit ${this._timeoutMs}ms); ${this._count} heartbeat(s), last: ${this._lastReason}`
    );
    try {
      if (this._onTimeout) {
        await Promise.race([this._onTimeout(), new Promise((resolve) => setTimeout(resolve, 3000))]);
      }
    } catch {}
    process.exit(1);
  }
}

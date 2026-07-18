/**
 * event-probe-awaits.js — Async wait/assert helpers for EventProbe.
 * Attached to the EventProbe prototype via mixin so the main class file
 * stays within the 200-line Kolmogorov line budget and individual helpers
 * stay within the 60-line function budget.
 */

const DEFAULT_AWAIT_TIMEOUT_MS = 30000;
const DEFAULT_NEVER_TIMEOUT_MS = 5000;
const TIMED_OUT_SENTINEL = 'timed out';

function removeCallback(probe, callback) {
  const idx = probe._onEventCallbacks.indexOf(callback);
  if (idx >= 0) probe._onEventCallbacks.splice(idx, 1);
}

function makeAwaitTimeout(probe, callback, predicate, timeoutMs, resolve, reject) {
  return setTimeout(() => {
    removeCallback(probe, callback);
    reject(new Error(`awaitEvent timed out after ${timeoutMs}ms`));
  }, timeoutMs);
}

function bindAwaitCallback(probe, predicate, timeoutMs, resolve, reject) {
  const callback = (event) => {
    if (predicate(event)) {
      clearTimeout(probe._awaitTimer);
      removeCallback(probe, callback);
      resolve(event);
    }
  };
  probe._awaitTimer = makeAwaitTimeout(
    probe, callback, predicate, timeoutMs, resolve, reject,
  );
  probe._onEventCallbacks.push(callback);
}

export function attachEventProbeAwaits(proto) {
  proto.awaitEvent = function awaitEvent(predicate, timeoutMs = DEFAULT_AWAIT_TIMEOUT_MS) {
    const existing = this._events.find(predicate);
    if (existing) return Promise.resolve(existing);
    return new Promise((resolve, reject) => {
      bindAwaitCallback(this, predicate, timeoutMs, resolve, reject);
    });
  };

  proto.awaitSequence = async function awaitSequence(predicates, timeoutMs) {
    const results = [];
    for (const pred of predicates) {
      results.push(await this.awaitEvent(pred, timeoutMs));
    }
    return results;
  };

  proto.assertNever = async function assertNever(predicate, timeoutMs = DEFAULT_NEVER_TIMEOUT_MS) {
    const existing = this._events.find(predicate);
    if (existing) {
      throw new Error(`assertNever failed: event matched at seq=${existing.seq} type=${existing.type}`);
    }
    if (timeoutMs <= 0) return;
    try {
      await this.awaitEvent(predicate, timeoutMs);
      throw new Error('assertNever failed: event appeared within wait window');
    } catch (err) {
      if (err.message.includes(TIMED_OUT_SENTINEL)) return;
      throw err;
    }
  };

  proto.expectCount = function expectCount({ type, sessionID, count }) {
    const actual = this.count(type, sessionID);
    if (actual !== count) {
      throw new Error(`Expected ${count} event(s) of type ${type} for session ${sessionID}, got ${actual}`);
    }
  };

  proto.dump = function dump(n = 100) {
    const tail = this._events.slice(-n);
    return tail.map(e => formatDumpLine(e)).join('\n');
  };
}

function formatDumpLine(e) {
  const parts = [
    `#${e.seq}`,
    new Date(e.time).toISOString().slice(11, 23),
    e.type,
  ];
  if (e.sessionID) parts.push(`session=${String(e.sessionID).slice(0, 12)}`);
  if (e.messageID) parts.push(`msg=${String(e.messageID).slice(0, 12)}`);
  if (e.error) parts.push(`error=${typeof e.error === 'string' ? e.error : JSON.stringify(e.error)}`);
  if (e.finishReason) parts.push(`finish=${e.finishReason}`);
  return parts.join(' ');
}

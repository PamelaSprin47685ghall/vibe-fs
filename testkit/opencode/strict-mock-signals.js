export class StrictMockSignals {
  constructor() {
    this._activeResponses = new Set();
    /** @type {Set<string>} permanently satisfied one-shot expectation ids */
    this._consumed = new Set();
    /** @type {Map<string, number>} match counts (reusable edges keep rising) */
    this._matchCount = new Map();
    this._expectationWaiters = new Map();
    this._idleWaiters = new Set();
    this._fatalError = null;
  }

  trackResponse(response) {
    this._activeResponses.add(response);
    response.on('close', () => {
      this._activeResponses.delete(response);
      if (this._activeResponses.size === 0) this._resolveIdle();
    });
  }

  /**
   * Record one match of `id` and wake current waiters.
   * - permanent: one-shot path edges. Future wait(id) returns immediately.
   * - non-permanent (reusable): only this generation of waiters wakes; a later
   *   wait(id) blocks until the *next* match. Used so dual-PERFECT barriers
   *   insert intermediate causal events without raising wall-clock timeouts.
   */
  consume(expectation) {
    const id = expectation.id;
    const permanent = expectation.permanent === true;
    this._matchCount.set(id, (this._matchCount.get(id) || 0) + 1);
    if (permanent) this._consumed.add(id);
    this._resolveWaiters(this._expectationWaiters.get(id));
    this._expectationWaiters.delete(id);
  }

  hasConsumed(id) {
    return this._consumed.has(id);
  }

  matchCount(id) {
    return this._matchCount.get(id) || 0;
  }

  /**
   * Wait for the next match of `id`.
   * One-shot (already permanently consumed): resolve immediately.
   * Reusable: always wait for a new match after this call is registered
   * (prior matches do not satisfy a later wait).
   */
  waitForExpectation(id, timeoutMs) {
    if (this._fatalError) return Promise.reject(this._fatalError);
    if (this._consumed.has(id)) return Promise.resolve();
    return this._wait(this._expectationWaiters, id, timeoutMs, `expectation ${id}`);
  }

  waitForIdle(timeoutMs) {
    if (this._fatalError) return Promise.reject(this._fatalError);
    if (this._activeResponses.size === 0) return Promise.resolve();
    return this._waitIdle(timeoutMs);
  }

  /**
   * First script mismatch: reject every waiter so the canary cannot continue
   * waiting. Does not destroy the HTTP response that is reporting the 500.
   */
  fail(error) {
    this._fatalError = error instanceof Error ? error : new Error(String(error));
    for (const waiters of this._expectationWaiters.values()) {
      this._rejectWaiters(waiters, this._fatalError);
    }
    this._expectationWaiters.clear();
    this._rejectWaiters(this._idleWaiters, this._fatalError);
    this._idleWaiters.clear();
  }

  stop() {
    for (const response of this._activeResponses) {
      try { response.destroy(); } catch {}
    }
    this._activeResponses.clear();
    this._resolveIdle();
  }

  get activeRequestCount() {
    return this._activeResponses.size;
  }

  _wait(store, key, timeoutMs, label) {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        waiters.delete(entry);
        reject(new Error(`Timed out waiting for ${label}`));
      }, timeoutMs);
      const entry = {
        resolve: () => {
          clearTimeout(timeout);
          waiters.delete(entry);
          resolve();
        },
        reject: (err) => {
          clearTimeout(timeout);
          waiters.delete(entry);
          reject(err);
        },
      };
      const waiters = store.get(key) || new Set();
      waiters.add(entry);
      store.set(key, waiters);
    });
  }

  _waitIdle(timeoutMs) {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        this._idleWaiters.delete(entry);
        reject(new Error('Timed out waiting for mock provider idle'));
      }, timeoutMs);
      const entry = {
        resolve: () => {
          clearTimeout(timeout);
          this._idleWaiters.delete(entry);
          resolve();
        },
        reject: (err) => {
          clearTimeout(timeout);
          this._idleWaiters.delete(entry);
          reject(err);
        },
      };
      this._idleWaiters.add(entry);
    });
  }

  _resolveWaiters(waiters) {
    if (!waiters) return;
    for (const entry of [...waiters]) entry.resolve();
  }

  _rejectWaiters(waiters, err) {
    if (!waiters) return;
    for (const entry of [...waiters]) entry.reject(err);
  }

  _resolveIdle() {
    this._resolveWaiters(this._idleWaiters);
    this._idleWaiters.clear();
  }
}

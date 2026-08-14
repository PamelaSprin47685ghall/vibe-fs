export class StrictMockSignals {
  constructor() {
    this._activeResponses = new Set();
    /** @type {Set<string>} permanently satisfied one-shot expectation ids */
    this._consumed = new Set();
    /** @type {Map<string, number>} total matches observed for each wait id */
    this._matchCount = new Map();
    /**
     * How many matches have already been claimed by waitForExpectation.
     * Reusable / neverEnd edges keep matchCount rising; each wait claims exactly
     * one match. If a match arrives before wait is registered, the next wait
     * claims the buffered match (intermediate causal event, no timeout raise).
     * @type {Map<string, number>}
     */
    this._claimCount = new Map();
    this._expectationWaiters = new Map();
    this._expectationAttemptWaiters = new Map();
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
   * permanent=true: one-shot path edge; future wait(id) returns immediately.
   * permanent=false: reusable / neverEnd / pathless; each wait claims one match.
   */
  consume(expectation) {
    const id = expectation.id;
    const permanent = expectation.permanent === true;
    this._matchCount.set(id, (this._matchCount.get(id) || 0) + 1);
    if (permanent) this._consumed.add(id);
    this._resolveWaiters(this._expectationWaiters.get(id));
    // Do not delete the waiter set before resolve — _resolveWaiters iterates a copy.
    this._expectationWaiters.delete(id);
    this._resolveAttemptWaiters(id);
  }

  hasConsumed(id) {
    return this._consumed.has(id);
  }

  matchCount(id) {
    return this._matchCount.get(id) || 0;
  }

  claimCount(id) {
    return this._claimCount.get(id) || 0;
  }

  /**
   * Claim the next match of `id`.
   * - One-shot already permanent: resolve immediately.
   * - Buffered reusable match (matchCount > claimCount): claim one and resolve.
   * - Otherwise wait for the next match, then claim one.
   */
  waitForExpectation(id, timeoutMs) {
    if (this._fatalError) return Promise.reject(this._fatalError);
    if (this._consumed.has(id)) return Promise.resolve();
    if (this.matchCount(id) > this.claimCount(id)) {
      this._claimCount.set(id, this.claimCount(id) + 1);
      return Promise.resolve();
    }
    return this._wait(this._expectationWaiters, id, timeoutMs, `expectation ${id}`).then(() => {
      // Match may have been permanent-consumed while we waited.
      if (this._consumed.has(id)) return;
      if (this.matchCount(id) > this.claimCount(id)) {
        this._claimCount.set(id, this.claimCount(id) + 1);
      }
    });
  }

  waitForExpectationAttempt(id, attempts, timeoutMs) {
    if (!Number.isInteger(attempts) || attempts < 1) {
      return Promise.reject(new Error(`expectation attempts must be a positive integer: ${attempts}`));
    }
    if (this._fatalError) return Promise.reject(this._fatalError);
    if (this.matchCount(id) >= attempts) return Promise.resolve();

    return new Promise((resolve, reject) => {
      const waiters = this._expectationAttemptWaiters.get(id) || new Set();
      let timeout;
      const remove = () => {
        waiters.delete(entry);
        if (waiters.size === 0) this._expectationAttemptWaiters.delete(id);
      };
      const entry = {
        attempts,
        resolve: () => {
          clearTimeout(timeout);
          remove();
          resolve();
        },
        reject: (error) => {
          clearTimeout(timeout);
          remove();
          reject(error);
        },
      };
      if (timeoutMs !== undefined) {
        timeout = setTimeout(() => {
          remove();
          reject(new Error(`Timed out waiting for expectation ${id} attempt ${attempts}`));
        }, timeoutMs);
      }
      waiters.add(entry);
      this._expectationAttemptWaiters.set(id, waiters);
    });
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
    for (const waiters of this._expectationAttemptWaiters.values()) {
      this._rejectWaiters(waiters, this._fatalError);
    }
    this._expectationAttemptWaiters.clear();
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
      const waiters = store.get(key) || new Set();
      let timeout;
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
      if (timeoutMs !== undefined) {
        timeout = setTimeout(() => {
          waiters.delete(entry);
          reject(new Error(`Timed out waiting for ${label}`));
        }, timeoutMs);
      }
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

  _resolveAttemptWaiters(id) {
    const waiters = this._expectationAttemptWaiters.get(id);
    if (!waiters) return;
    for (const entry of [...waiters]) {
      if (this.matchCount(id) >= entry.attempts) entry.resolve();
    }
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

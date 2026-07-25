export class StrictMockSignals {
  constructor() {
    this._activeResponses = new Set();
    this._consumed = new Set();
    this._expectationWaiters = new Map();
    this._idleWaiters = new Set();
  }

  trackResponse(response) {
    this._activeResponses.add(response);
    response.on('close', () => {
      this._activeResponses.delete(response);
      if (this._activeResponses.size === 0) this._resolve(this._idleWaiters);
    });
  }

  consume(expectation) {
    this._consumed.add(expectation.id);
    this._resolve(this._expectationWaiters.get(expectation.id));
    this._expectationWaiters.delete(expectation.id);
  }

  hasConsumed(id) {
    return this._consumed.has(id);
  }

  waitForExpectation(id, timeoutMs) {
    if (this._consumed.has(id)) return Promise.resolve();
    return this._wait(this._expectationWaiters, id, timeoutMs, `expectation ${id}`);
  }

  waitForIdle(timeoutMs) {
    if (this._activeResponses.size === 0) return Promise.resolve();
    return this._wait({ get: () => this._idleWaiters, set: () => {} }, 'idle', timeoutMs, 'mock provider idle');
  }

  stop() {
    for (const response of this._activeResponses) {
      try { response.destroy(); } catch {}
    }
    this._activeResponses.clear();
    this._resolve(this._idleWaiters);
  }

  get activeRequestCount() {
    return this._activeResponses.size;
  }

  _wait(store, key, timeoutMs, label) {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        waiters.delete(done);
        reject(new Error(`Timed out waiting for ${label}`));
      }, timeoutMs);
      const done = () => {
        clearTimeout(timeout);
        waiters.delete(done);
        resolve();
      };
      const waiters = store.get(key) || new Set();
      waiters.add(done);
      store.set(key, waiters);
    });
  }

  _resolve(waiters) {
    if (!waiters) return;
    for (const done of [...waiters]) done();
  }
}

/**
 * event-probe.js — Records and queries OpenCode SSE events for E2E assertions.
 *
 * Uses fetch + streaming response parsing instead of EventSource dependency.
 *
 * Usage:
 *   const probe = new EventProbe(baseUrl, workDir);
 *   await probe.connect();
 *   const idleEvents = probe.bySession(sessionId).filter(e => e.type === 'session.idle');
 *   await probe.awaitEvent(e => e.type === 'session.message');
 *   probe.assertNever(e => e.type === 'session.error');
 *   await probe.close();
 */

export class EventProbe {
  /**
   * @param {string} baseUrl - OpenCode server base URL
   * @param {string} workDir - Work directory
   */
  constructor(baseUrl, workDir) {
    this._baseUrl = baseUrl;
    this._workDir = workDir;
    this._events = [];
    this._seq = 0;
    this._reader = null;
    this._abortController = null;
    this._connected = false;
    this._onEventCallbacks = [];
    this._readPromise = null;
  }

  /**
   * Connect to the OpenCode event stream.
   */
  async connect() {
    if (this._connected) return;

    this._abortController = new AbortController();
    const url = `${this._baseUrl}/event`;

    try {
      const response = await fetch(url, {
        headers: {
          'Accept': 'text/event-stream',
          'x-opencode-directory': this._workDir,
        },
        signal: this._abortController.signal,
      });

      if (!response.ok) {
        throw new Error(`GET /event failed with status ${response.status}`);
      }

      this._connected = true;
      const reader = response.body.getReader();
      this._reader = reader;
      const decoder = new TextDecoder();
      let buffer = '';

      this._readPromise = this._readLoop(reader, decoder, buffer);
    } catch (err) {
      if (err.name === 'AbortError') return;
      throw err;
    }
  }

  async _readLoop(reader, decoder, buffer) {
    try {
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        buffer = this._processBuffer(buffer);
      }
      // Process remaining
      if (buffer.trim()) {
        this._processLine(buffer);
      }
    } catch (err) {
      if (err.name === 'AbortError' || this._abortController?.signal.aborted) {
        return;
      }
      throw err;
    }
  }

  _processBuffer(buffer) {
    const lines = buffer.split('\n');
    // Keep the last potentially incomplete line in the buffer
    const remaining = lines.pop() || '';
    for (const line of lines) {
      this._processLine(line);
    }
    return remaining;
  }

  _processLine(line) {
    const trimmed = line.trim();
    if (!trimmed.startsWith('data:')) return;
    const jsonStr = trimmed.slice(5).trim();
    if (!jsonStr) return;

    try {
      const parsed = JSON.parse(jsonStr);
      const eventObj = {
        seq: ++this._seq,
        time: Date.now(),
        raw: jsonStr,
      };

      if (parsed?.type) {
        eventObj.type = parsed.type;
        eventObj.properties = parsed.properties || {};
        eventObj.sessionID = parsed.properties?.sessionID;
        eventObj.messageID = parsed.properties?.messageID;
        eventObj.partID = parsed.properties?.partID;
        eventObj.toolCallID = parsed.properties?.toolCallID;
        eventObj.error = parsed.properties?.error;
        eventObj.finishReason = parsed.properties?.finishReason;
        eventObj.status = parsed.properties?.status;
      } else {
        eventObj.type = 'unknown';
        eventObj.properties = parsed;
      }

      this._events.push(eventObj);

      // Notify awaiters
      for (const cb of this._onEventCallbacks) {
        try { cb(eventObj); } catch {}
      }
    } catch (e) {
      this._events.push({
        seq: ++this._seq,
        time: Date.now(),
        type: 'parse-error',
        raw: jsonStr,
        error: e.message,
      });
    }
  }

  /**
   * Close the event stream connection.
   */
  close() {
    if (this._abortController) {
      this._abortController.abort();
      this._abortController = null;
    }
    this._reader = null;
    this._connected = false;
  }

  // ── Queries ──

  get allEvents() { return this._events; }

  bySession(sessionID) {
    return this._events.filter(e => e.sessionID === sessionID);
  }

  count(type, sessionID) {
    return this._events.filter(e => {
      if (e.type !== type) return false;
      if (sessionID !== undefined && e.sessionID !== sessionID) return false;
      return true;
    }).length;
  }

  // ── Async wait helpers ──

  awaitEvent(predicate, timeoutMs = 30000) {
    const existing = this._events.find(predicate);
    if (existing) return Promise.resolve(existing);

    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => {
        const idx = this._onEventCallbacks.indexOf(callback);
        if (idx >= 0) this._onEventCallbacks.splice(idx, 1);
        reject(new Error(`awaitEvent timed out after ${timeoutMs}ms`));
      }, timeoutMs);

      const callback = (event) => {
        if (predicate(event)) {
          clearTimeout(timeout);
          const idx = this._onEventCallbacks.indexOf(callback);
          if (idx >= 0) this._onEventCallbacks.splice(idx, 1);
          resolve(event);
        }
      };

      this._onEventCallbacks.push(callback);
    });
  }

  async awaitSequence(predicates, timeoutMs = 30000) {
    const results = [];
    for (const pred of predicates) {
      const event = await this.awaitEvent(pred, timeoutMs);
      results.push(event);
    }
    return results;
  }

  // ── Assertions ──

  async assertNever(predicate, timeoutMs = 5000) {
    const existing = this._events.find(predicate);
    if (existing) {
      throw new Error(`assertNever failed: event matched at seq=${existing.seq} type=${existing.type}`);
    }

    if (timeoutMs > 0) {
      try {
        await this.awaitEvent(predicate, timeoutMs);
        throw new Error('assertNever failed: event appeared within wait window');
      } catch (err) {
        if (err.message.includes('timed out')) {
          return;
        }
        throw err;
      }
    }
  }

  expectCount({ type, sessionID, count }) {
    const actual = this.count(type, sessionID);
    if (actual !== count) {
      throw new Error(`Expected ${count} event(s) of type ${type} for session ${sessionID}, got ${actual}`);
    }
  }

  // ── Diagnostics ──

  dump(n = 100) {
    const tail = this._events.slice(-n);
    return tail.map(e => {
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
    }).join('\n');
  }
}

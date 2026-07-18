/**
 * event-probe.js — Records and queries OpenCode SSE events for E2E assertions.
 *
 * Uses fetch + streaming response parsing instead of EventSource dependency.
 *
 * close() is async: awaits the SSE read loop to truly exit before resolving,
 * so callers can rely on a deterministic "no more events" point.
 *
 * State queries and async awaits are attached via mixin modules
 * (event-probe-queries.js / event-probe-awaits.js) so the main class
 * stays within the 200-line Kolmogorov line budget.
 */

import {
  shapeFromParsed,
  shapeFromParseError,
  shapeFromReadError,
} from './event-shape.js';
import { attachEventProbeQueries } from './event-probe-queries.js';
import { attachEventProbeAwaits } from './event-probe-awaits.js';

const READ_ERROR_FALLBACK = 'unknown read error';

export class EventProbe {
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
    this._readSettled = false;
    this._closeResolve = null;
    this._closePromise = new Promise((resolve) => {
      this._closeResolve = resolve;
    });
  }

  async connect() {
    if (this._connected) return;
    this._abortController = new AbortController();
    try {
      const response = await fetch(`${this._baseUrl}/event`, {
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
      this._readPromise = this._readLoop(reader, decoder);
    } catch (err) {
      if (err.name === 'AbortError') return;
      throw err;
    }
  }

  async _readLoop(reader, decoder) {
    let buffer = '';
    try {
      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += decoder.decode(value, { stream: true });
        buffer = this._processBuffer(buffer);
      }
      if (buffer.trim()) this._processLine(buffer);
    } catch (err) {
      if (!(err.name === 'AbortError' || this._abortController?.signal.aborted)) {
        this._events.push({
          seq: ++this._seq,
          time: Date.now(),
          ...shapeFromReadError(err.message || READ_ERROR_FALLBACK),
        });
      }
    } finally {
      this._readSettled = true;
      this._connected = false;
      const resolve = this._closeResolve;
      if (resolve) resolve();
    }
  }

  _processBuffer(buffer) {
    const lines = buffer.split('\n');
    const remaining = lines.pop() || '';
    for (const line of lines) this._processLine(line);
    return remaining;
  }

  _processLine(line) {
    const trimmed = line.trim();
    if (!trimmed.startsWith('data:')) return;
    const jsonStr = trimmed.slice(5).trim();
    if (!jsonStr) return;
    let parsed;
    try {
      parsed = JSON.parse(jsonStr);
    } catch (e) {
      this._events.push({
        seq: ++this._seq,
        time: Date.now(),
        ...shapeFromParseError(jsonStr, e.message),
      });
      return;
    }
    const eventObj = {
      seq: ++this._seq,
      time: Date.now(),
      raw: jsonStr,
      ...shapeFromParsed(parsed),
    };
    this._events.push(eventObj);
    for (const cb of this._onEventCallbacks) {
      try { cb(eventObj); } catch {}
    }
  }

  async close() {
    if (!this._abortController && this._readSettled) return;
    if (this._abortController) {
      this._abortController.abort();
      this._abortController = null;
    }
    this._reader = null;
    if (this._readPromise) {
      try { await this._readPromise; } catch {}
    } else if (!this._readSettled) {
      this._readSettled = true;
      const resolve = this._closeResolve;
      if (resolve) resolve();
    }
  }

  get allEvents() { return this._events; }
}

attachEventProbeQueries(EventProbe.prototype);
attachEventProbeAwaits(EventProbe.prototype);
